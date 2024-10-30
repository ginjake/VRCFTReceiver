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
    UniLog.Log("[VRCFTReceiver] Finished Initializing VRCFT driver");
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
    mouth.JawOpen = MathX.Clamp01(OSCClient.GetData(ExpressionIndex.JawOpen) - OSCClient.GetData(ExpressionIndex.MouthClosed));
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
    foreach (var profile in _OSCQuery.profiles)
    {
      if (profile.name.StartsWith("VRCFT"))
      {
        OSCClient.SendMessage(profile.address, profile.port, "/avatar/change", "default");
      }
    }
  }
}
