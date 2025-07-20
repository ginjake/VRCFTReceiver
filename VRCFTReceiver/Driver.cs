using System;
using System.Net;
using Elements.Core;
using FrooxEngine;
using Rug.Osc;

namespace VRCFTReceiver;

public class Driver : IInputDriver, IDisposable
{
  private InputInterface input;
  private Eyes eyes;
  private Mouth mouth;
  private OSCClient _OSCClient;
  private OSCQuery _OSCQuery;
  private IPAddress IP;
  private int ReceiverPort;
  private bool EnableEyeTracking;
  private bool EnableFaceTracking;
  private static int TrackingTimeout;
  public bool EyesReversedY = false;
  public bool EyesReversedX = false;
  public VRCFTEye EyeLeft = new();
  public VRCFTEye EyeRight = new();
  public VRCFTEye EyeCombined => new()
  {
    Eyelid = MathX.Max(EyeLeft.Eyelid, EyeRight.Eyelid),
    EyeRotation = CombinedEyesDir
  };
  public floatQ CombinedEyesDir
  {
    get
    {
      if (EyeLeft.IsValid && EyeRight.IsValid && EyeLeft.IsTracking && EyeRight.IsTracking)
        _lastValidCombined = MathX.Slerp(EyeLeft.EyeRotation, EyeRight.EyeRotation, 0.5f);
      else if (EyeLeft.IsValid && EyeLeft.IsTracking)
        _lastValidCombined = EyeLeft.EyeRotation;
      else if (EyeRight.IsValid && EyeRight.IsTracking)
        _lastValidCombined = EyeRight.EyeRotation;

      return _lastValidCombined;
    }
  }
  private floatQ _lastValidCombined = floatQ.Identity;
  public int UpdateOrder => 100;
  public void CollectDeviceInfos(DataTreeList list)
  {
    DataTreeDictionary eyeDict = new();
    eyeDict.Add("Name", "VRCFaceTracking OSC");
    eyeDict.Add("Type", "Eye Tracking");
    eyeDict.Add("Model", "VRCFaceTracking OSC");
    list.Add(eyeDict);
    DataTreeDictionary mouthDict = new();
    mouthDict.Add("Name", "VRCFaceTracking OSC");
    mouthDict.Add("Type", "Lip Tracking");
    mouthDict.Add("Model", "VRCFaceTracking OSC");
    list.Add(mouthDict);
  }
  public void RegisterInputs(InputInterface inputInterface)
  {
    try
    {
      input = inputInterface;
      eyes = new Eyes(inputInterface, "VRCFaceTracking OSC", supportsPupilTracking: false);
      mouth = new Mouth(inputInterface, "VRCFaceTracking OSC", new MouthParameterGroup[16]
      {
        MouthParameterGroup.JawPose,
        MouthParameterGroup.JawOpen,
        MouthParameterGroup.TonguePose,
        MouthParameterGroup.LipRaise,
        MouthParameterGroup.LipHorizontal,
        MouthParameterGroup.SmileFrown,
        MouthParameterGroup.MouthDimple,
        MouthParameterGroup.MouthPout,
        MouthParameterGroup.LipOverturn,
        MouthParameterGroup.LipOverUnder,
        MouthParameterGroup.LipStretchTighten,
        MouthParameterGroup.LipsPress,
        MouthParameterGroup.CheekPuffSuck,
        MouthParameterGroup.CheekRaise,
        MouthParameterGroup.ChinRaise,
        MouthParameterGroup.NoseWrinkle
      });
      OnSettingsChanged();
      VRCFTReceiver.config.OnThisConfigurationChanged += (_) => OnSettingsChanged();
      input.Engine.OnShutdown += Dispose;
      
      // Send initial avatar change immediately
      AvatarChange(null);
      
      UniLog.Log("[VRCFTReceiver] Finished Initializing VRCFT driver");
    }
    catch (Exception ex)
    {
      UniLog.Error($"[VRCFTReceiver] Failed to register inputs: {ex}");
      throw;
    }
  }
  
  private void OnSettingsChanged()
  {
    EnableEyeTracking = VRCFTReceiver.config.GetValue(VRCFTReceiver.ENABLE_EYE_TRACKING);
    EnableFaceTracking = VRCFTReceiver.config.GetValue(VRCFTReceiver.ENABLE_FACE_TRACKING);
    ReceiverPort = VRCFTReceiver.config.GetValue(VRCFTReceiver.KEY_RECEIVER_PORT);
    IP = IPAddress.Parse(VRCFTReceiver.config.GetValue(VRCFTReceiver.KEY_IP));
    EyesReversedY = VRCFTReceiver.config.GetValue(VRCFTReceiver.REVERSE_EYES_Y);
    EyesReversedX = VRCFTReceiver.config.GetValue(VRCFTReceiver.REVERSE_EYES_X);
    TrackingTimeout = VRCFTReceiver.config.GetValue(VRCFTReceiver.TRACKING_TIMEOUT_SECONDS);
    UniLog.Log($"[VRCFTReceiver] Starting VRCFTReceiver with these settings: EnableEyeTracking: {EnableEyeTracking}, EnableFaceTracking: {EnableFaceTracking},  ReceiverPort:{ReceiverPort}, IP: {IP}, EyesReversedY: {EyesReversedY}, EyesReversedX: {EyesReversedX}, TrackingTimeout: {TrackingTimeout}");
    InitializeOSCConnection();
  }
  private void InitializeOSCConnection()
  {
    UniLog.Log("[VRCFTReceiver] Initializing OSCConnection...");
    if (ReceiverPort != 0 && IP != null)
    {
      try
      {
        if (_OSCClient != null) _OSCClient.Teardown();
        _OSCClient = new OSCClient(IP, ReceiverPort);
        if (_OSCQuery != null) _OSCQuery.Teardown();
        _OSCQuery = new OSCQuery(ReceiverPort);
      }
      catch (Exception ex)
      {
        UniLog.Error("[VRCFTReceiver] Exception when starting OSCConnection:\n" + ex);
      }
    }
    else
    {
      UniLog.Warning("[VRCFTReceiver] OSCConnection not started because port or IP is not valid");
    }
  }
  public void UpdateInputs(float deltaTime)
  {
    bool isEyeTracking = EnableEyeTracking && IsTracking(OSCClient.LastEyeTracking);
    bool isFaceTracking = EnableFaceTracking && IsTracking(OSCClient.LastFaceTracking);

    try
    {
      if (isEyeTracking)
      {
        UpdateEyes(deltaTime);
      }
      else
      {
        eyes.IsEyeTrackingActive = false;
        eyes.SetTracking(state: false);
      }

      if (isFaceTracking)
      {
        UpdateMouth(deltaTime);
      }
      else
      {
        mouth.IsTracking = false;
        mouth.IsDeviceActive = false;
      }
    }
    catch (Exception ex)
    {
      UniLog.Error($"[VRCFTReceiver] UpdateInputs Failed! Exception: {ex}");
    }
  }
  private void UpdateEyes(float deltaTime)
  {
    eyes.IsEyeTrackingActive = true;
    eyes.SetTracking(state: true);

    EyeLeft.SetDirectionFromXY(
      X: EyesReversedX ? -OSCClient.GetData(ExpressionIndex.EyeLeftX) : OSCClient.GetData(ExpressionIndex.EyeLeftX),
      Y: EyesReversedY ? -OSCClient.GetData(ExpressionIndex.EyeLeftY) : OSCClient.GetData(ExpressionIndex.EyeLeftY)
    );
    EyeRight.SetDirectionFromXY(
      X: EyesReversedX ? -OSCClient.GetData(ExpressionIndex.EyeRightX) : OSCClient.GetData(ExpressionIndex.EyeRightX),
      Y: EyesReversedY ? -OSCClient.GetData(ExpressionIndex.EyeRightY) : OSCClient.GetData(ExpressionIndex.EyeRightY)
    );

    UpdateEye(EyeLeft, eyes.LeftEye);
    UpdateEye(EyeRight, eyes.RightEye);
    UpdateEye(EyeCombined, eyes.CombinedEye);

    eyes.LeftEye.Openness = OSCClient.GetData(ExpressionIndex.EyeOpenLeft);
    eyes.RightEye.Openness = OSCClient.GetData(ExpressionIndex.EyeOpenRight);
    eyes.LeftEye.Widen = OSCClient.GetData(ExpressionIndex.EyeWideLeft);
    eyes.RightEye.Widen = OSCClient.GetData(ExpressionIndex.EyeWideRight);
    eyes.LeftEye.Squeeze = OSCClient.GetData(ExpressionIndex.EyeSquintLeft);
    eyes.RightEye.Squeeze = OSCClient.GetData(ExpressionIndex.EyeSquintRight);

    float leftBrowLowerer = OSCClient.GetData(ExpressionIndex.BrowPinchLeft) - OSCClient.GetData(ExpressionIndex.BrowLowererLeft);
    eyes.LeftEye.InnerBrowVertical = OSCClient.GetData(ExpressionIndex.BrowInnerUpLeft) - leftBrowLowerer;
    eyes.LeftEye.OuterBrowVertical = OSCClient.GetData(ExpressionIndex.BrowOuterUpLeft) - leftBrowLowerer;

    float rightBrowLowerer = OSCClient.GetData(ExpressionIndex.BrowPinchRight) - OSCClient.GetData(ExpressionIndex.BrowLowererRight);
    eyes.RightEye.InnerBrowVertical = OSCClient.GetData(ExpressionIndex.BrowInnerUpRight) - rightBrowLowerer;
    eyes.RightEye.OuterBrowVertical = OSCClient.GetData(ExpressionIndex.BrowOuterUpRight) - rightBrowLowerer;

    eyes.ComputeCombinedEyeParameters();
    eyes.FinishUpdate();
  }
  public void UpdateEye(VRCFTEye source, Eye dest)
  {
    if (source.IsValid)
    {
      dest.UpdateWithRotation(source.EyeRotation);
    }
  }
  private void UpdateMouth(float deltaTime)
  {
    mouth.IsTracking = true;
    mouth.IsDeviceActive = true;

    mouth.MouthLeftSmileFrown = OSCClient.GetData(ExpressionIndex.MouthSmileLeft) - OSCClient.GetData(ExpressionIndex.MouthFrownLeft);
    mouth.MouthRightSmileFrown = OSCClient.GetData(ExpressionIndex.MouthSmileRight) - OSCClient.GetData(ExpressionIndex.MouthFrownRight);
    mouth.MouthLeftDimple = OSCClient.GetData(ExpressionIndex.MouthDimpleLeft);
    mouth.MouthRightDimple = OSCClient.GetData(ExpressionIndex.MouthDimpleRight);
    mouth.CheekLeftPuffSuck = OSCClient.GetData(ExpressionIndex.CheekPuffSuckLeft);
    mouth.CheekRightPuffSuck = OSCClient.GetData(ExpressionIndex.CheekPuffSuckRight);
    mouth.CheekLeftRaise = OSCClient.GetData(ExpressionIndex.CheekSquintLeft);
    mouth.CheekRightRaise = OSCClient.GetData(ExpressionIndex.CheekSquintRight);
    mouth.LipUpperLeftRaise = OSCClient.GetData(ExpressionIndex.MouthUpperUpLeft);
    mouth.LipUpperRightRaise = OSCClient.GetData(ExpressionIndex.MouthUpperUpRight);
    mouth.LipLowerLeftRaise = OSCClient.GetData(ExpressionIndex.MouthLowerDownLeft);
    mouth.LipLowerRightRaise = OSCClient.GetData(ExpressionIndex.MouthLowerDownRight);
    mouth.MouthPoutLeft = OSCClient.GetData(ExpressionIndex.LipPuckerUpperLeft) - OSCClient.GetData(ExpressionIndex.LipPuckerLowerLeft);
    mouth.MouthPoutRight = OSCClient.GetData(ExpressionIndex.LipPuckerUpperRight) - OSCClient.GetData(ExpressionIndex.LipPuckerLowerRight);
    mouth.LipUpperHorizontal = OSCClient.GetData(ExpressionIndex.MouthUpperX);
    mouth.LipLowerHorizontal = OSCClient.GetData(ExpressionIndex.MouthLowerX);
    mouth.LipTopLeftOverturn = OSCClient.GetData(ExpressionIndex.LipFunnelUpperLeft);
    mouth.LipTopRightOverturn = OSCClient.GetData(ExpressionIndex.LipFunnelUpperRight);
    mouth.LipBottomLeftOverturn = OSCClient.GetData(ExpressionIndex.LipFunnelLowerLeft);
    mouth.LipBottomRightOverturn = OSCClient.GetData(ExpressionIndex.LipFunnelLowerRight);
    mouth.LipTopLeftOverUnder = -OSCClient.GetData(ExpressionIndex.LipSuckUpperLeft);
    mouth.LipTopRightOverUnder = -OSCClient.GetData(ExpressionIndex.LipSuckUpperRight);
    mouth.LipBottomLeftOverUnder = -OSCClient.GetData(ExpressionIndex.LipSuckLowerLeft);
    mouth.LipBottomRightOverUnder = -OSCClient.GetData(ExpressionIndex.LipSuckLowerRight);
    mouth.LipLeftStretchTighten = OSCClient.GetData(ExpressionIndex.MouthStretchLeft) - OSCClient.GetData(ExpressionIndex.MouthTightenerLeft);
    mouth.LipRightStretchTighten = OSCClient.GetData(ExpressionIndex.MouthStretchRight) - OSCClient.GetData(ExpressionIndex.MouthTightenerRight);
    mouth.LipsLeftPress = OSCClient.GetData(ExpressionIndex.MouthPressLeft);
    mouth.LipsRightPress = OSCClient.GetData(ExpressionIndex.MouthPressRight);
    mouth.Jaw = new float3(
      OSCClient.GetData(ExpressionIndex.JawRight) - OSCClient.GetData(ExpressionIndex.JawLeft),
      -OSCClient.GetData(ExpressionIndex.MouthClosed),
      OSCClient.GetData(ExpressionIndex.JawForward)
    );
        mouth.JawOpen = MathX.Clamp01(OSCClient.GetData(ExpressionIndex.JawOpen) - OSCClient.GetData(ExpressionIndex.MouthClosed)) ;
    mouth.Tongue = new float3(
      OSCClient.GetData(ExpressionIndex.TongueX),
      OSCClient.GetData(ExpressionIndex.TongueY),
      OSCClient.GetData(ExpressionIndex.TongueOut)
    );
    mouth.TongueRoll = OSCClient.GetData(ExpressionIndex.TongueRoll);
    mouth.NoseWrinkleLeft = OSCClient.GetData(ExpressionIndex.NoseSneerLeft);
    mouth.NoseWrinkleRight = OSCClient.GetData(ExpressionIndex.NoseSneerRight);
    mouth.ChinRaiseBottom = OSCClient.GetData(ExpressionIndex.MouthRaiserLower);
    mouth.ChinRaiseTop = OSCClient.GetData(ExpressionIndex.MouthRaiserUpper);
  }
  public void Dispose()
  {
    UniLog.Log("[VRCFTReceiver] Driver disposal called");
    _OSCClient?.Teardown();
    _OSCQuery?.Teardown();
    UniLog.Log("[VRCFTReceiver] Driver disposed");
  }
  private static bool IsTracking(DateTime? timestamp)
  {
    if (!timestamp.HasValue)
    {
      return false;
    }
    if ((DateTime.UtcNow - timestamp.Value).TotalSeconds > TrackingTimeout)
    {
      return false;
    }
    return true;
  }
  public void AvatarChange()
  {
    AvatarChange(null);
  }

  public void AvatarChange(UserRoot userRoot)
  {
    // Use simple, consistent avatar information like the working version
    string avatarId = "avtr_c38a1615-e776-4611-a524-c0b00e6bb627"; // Fixed VRChat-style ID
    string avatarName = "ResoniteAvatar";
    
    try
    {
      if (userRoot != null && userRoot.ActiveUser != null && userRoot.ActiveUser.IsLocalUser)
      {
        var activeUser = userRoot.ActiveUser;
        
        // Use simple, reliable identification
        if (!string.IsNullOrEmpty(activeUser.UserName))
        {
          avatarName = $"{activeUser.UserName}_Avatar";
          // Generate consistent VRChat-style ID
          var hash = activeUser.UserName.GetHashCode();
          avatarId = $"avtr_{Math.Abs(hash):x8}-e776-4611-a524-c0b00e6bb627";
        }
        
        UniLog.Log($"[VRCFTReceiver] Avatar detected: Name='{avatarName}', ID='{avatarId}'");
      }
      else
      {
        UniLog.Warning("[VRCFTReceiver] UserRoot or ActiveUser is null");
      }
    }
    catch (Exception ex)
    {
      UniLog.Error($"[VRCFTReceiver] Failed to get avatar info: {ex.Message}");
    }

    // Send VRChat-compatible OSC messages
    var messagesSent = 0;
    foreach (var profile in _OSCQuery.profiles)
    {
      if (profile.name.StartsWith("VRCFT"))
      {
        try
        {
          // Primary VRChat avatar change message - this is critical
          OSCClient.SendMessage(profile.address, profile.port, "/avatar/change", avatarId);
          messagesSent++;
          
          UniLog.Log($"[VRCFTReceiver] Sent avatar change to {profile.name}:{profile.port} -> {avatarId}");
        }
        catch (Exception ex)
        {
          UniLog.Error($"[VRCFTReceiver] Failed to send avatar change: {ex.Message}");
        }
      }
    }
    
    // Also send to common VRCFaceTracking ports as backup
    try
    {
      OSCClient.SendMessage(IP, 9001, "/avatar/change", avatarId);
      OSCClient.SendMessage(IP, 9000, "/avatar/change", avatarId);
      messagesSent += 2;
      
      UniLog.Log($"[VRCFTReceiver] Sent backup avatar change messages to ports 9000/9001");
    }
    catch (Exception ex)
    {
      UniLog.Warning($"[VRCFTReceiver] Failed to send backup messages: {ex.Message}");
    }
    
    if (messagesSent == 0)
    {
      UniLog.Warning("[VRCFTReceiver] No avatar change messages were sent! Check OSCQuery profiles.");
    }
    else
    {
      UniLog.Log($"[VRCFTReceiver] Successfully sent {messagesSent} avatar change messages");
    }
  }
  
  private Slot FindAvatarSlot(Slot parentSlot)
  {
    if (parentSlot == null) return null;
    
    // Check if current slot looks like an avatar
    if (parentSlot.Name != null && 
        (parentSlot.Name.ToLower().Contains("avatar") || 
         parentSlot.Name.ToLower().Contains("character")))
    {
      return parentSlot;
    }
    
    // Search children recursively (limit depth to avoid infinite loops)
    return SearchAvatarInChildren(parentSlot, 0, 5);
  }
  
  private Slot SearchAvatarInChildren(Slot slot, int currentDepth, int maxDepth)
  {
    if (slot == null || currentDepth >= maxDepth) return null;
    
    for (int i = 0; i < slot.ChildrenCount; i++)
    {
      var child = slot[i];
      if (child?.Name != null && 
          (child.Name.ToLower().Contains("avatar") || 
           child.Name.ToLower().Contains("character")))
      {
        return child;
      }
      
      // Recursive search
      var result = SearchAvatarInChildren(child, currentDepth + 1, maxDepth);
      if (result != null) return result;
    }
    
    return null;
  }
  
  private Slot FindEnhancedAvatarSlot(Slot parentSlot)
  {
    if (parentSlot == null) return null;
    
    // Enhanced patterns for avatar detection
    var avatarPatterns = new[] { "avatar", "character", "model", "body", "mesh", "armature" };
    
    // Check current slot
    if (parentSlot.Name != null)
    {
      var lowerName = parentSlot.Name.ToLower();
      foreach (var pattern in avatarPatterns)
      {
        if (lowerName.Contains(pattern))
        {
          return parentSlot;
        }
      }
    }
    
    // Enhanced recursive search with more patterns
    return SearchEnhancedAvatarInChildren(parentSlot, 0, 7, avatarPatterns);
  }
  
  private Slot SearchEnhancedAvatarInChildren(Slot slot, int currentDepth, int maxDepth, string[] patterns)
  {
    if (slot == null || currentDepth >= maxDepth) return null;
    
    for (int i = 0; i < slot.ChildrenCount; i++)
    {
      var child = slot[i];
      if (child?.Name != null)
      {
        var lowerName = child.Name.ToLower();
        foreach (var pattern in patterns)
        {
          if (lowerName.Contains(pattern))
          {
            return child;
          }
        }
      }
      
      // Recursive search
      var result = SearchEnhancedAvatarInChildren(child, currentDepth + 1, maxDepth, patterns);
      if (result != null) return result;
    }
    
    return null;
  }
  
  private string ExtractAvatarIdFromUrl(string url)
  {
    if (string.IsNullOrEmpty(url)) return "default";
    
    // Try to extract VRChat-style avatar ID from URL
    try
    {
      var uri = new System.Uri(url);
      var pathSegments = uri.AbsolutePath.Split('/');
      
      foreach (var segment in pathSegments)
      {
        if (segment.StartsWith("avtr_") && segment.Length > 10)
        {
          return segment;
        }
      }
      
      // Fallback: generate ID from URL hash
      var hash = url.GetHashCode().ToString("X");
      return $"avtr_{hash}";
    }
    catch
    {
      // If URL parsing fails, generate from string hash
      var hash = url.GetHashCode().ToString("X");
      return $"avtr_{hash}";
    }
  }
  
  private string GenerateAvatarId(string avatarName)
  {
    if (string.IsNullOrEmpty(avatarName)) return "default";
    
    // Generate VRChat-style avatar ID
    var hash = avatarName.GetHashCode().ToString("X");
    var guid = System.Guid.NewGuid().ToString("N").Substring(0, 16);
    return $"avtr_{hash}_{guid}";
  }
  
  private string FindAvatarUrlInSlot(Slot slot)
  {
    if (slot == null) return "";
    
    // Generate URL from slot information using available properties
    try
    {
      // Use slot name and hierarchy to create unique identifier
      if (!string.IsNullOrEmpty(slot.Name))
      {
        var hash = slot.Name.GetHashCode().ToString("X");
        return $"resonite://avatar/{hash}";
      }
      
      // Check for URL in parent hierarchy using available properties
      var parent = slot.Parent;
      while (parent != null)
      {
        if (!string.IsNullOrEmpty(parent.Name))
        {
          var hash = parent.Name.GetHashCode().ToString("X");
          return $"resonite://avatar/{hash}";
        }
        parent = parent.Parent;
      }
    }
    catch (Exception)
    {
      // Ignore errors
    }
    
    return "";
  }
}
