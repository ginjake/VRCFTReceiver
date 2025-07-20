using HarmonyLib;
using ResoniteModLoader;
using FrooxEngine;
using System;
using System.Linq;
using System.Reflection;
using Elements.Core;

namespace VRCFTReceiver
{
    public class VRCFTReceiver : ResoniteMod
    {
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<string> KEY_IP = new("osc_ip", "IP Address of OSC Server", () => "127.0.0.1");
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<int> KEY_RECEIVER_PORT = new("receiver_port", "Which port should the OSC data be received from?", () => 9000);
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> ENABLE_EYE_TRACKING = new("enable_eye_tracking", "Enable eye tracking?", () => true);
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> ENABLE_FACE_TRACKING = new("enable_face_tracking", "Enable mouth tracking?", () => true);
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> REVERSE_EYES_Y = new("reverse_eyes_y", "Reverse eye tracking y direction", () => false);
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> REVERSE_EYES_X = new("reverse_eyes_x", "Reverse eye tracking x direction", () => false);
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<int> TRACKING_TIMEOUT_SECONDS = new("tracking_timeout_seconds", "Seconds until tracking is considered inactive", () => 5);
        public static ModConfiguration config;
        public static Driver VRCFTDriver;
        public override string Name => "VRCFTReceiver";
        public override string Author => "hazre";
        public override string Version => "2.0.0";
        public override string Link => "https://github.com/hazre/VRCFTReceiver";

        public override void OnEngineInit()
        {
            try
            {
                UniLog.Log("[VRCFTReceiver] === DIAGNOSIS: OnEngineInit START ===");
                config = GetConfiguration();
                
                UniLog.Log("[VRCFTReceiver] Creating Harmony instance...");
                Harmony harmony = new Harmony("dev.hazre.VRCFTReceiver");
                
                UniLog.Log("[VRCFTReceiver] Applying Harmony patches...");
                harmony.PatchAll();
                
                // Get applied patches info
                var allPatches = Harmony.GetAllPatchedMethods().Where(m => 
                    Harmony.GetPatchInfo(m).Owners.Contains("dev.hazre.VRCFTReceiver")).ToArray();
                UniLog.Log($"[VRCFTReceiver] Applied {allPatches.Length} patches:");
                foreach (var method in allPatches)
                {
                    UniLog.Log($"[VRCFTReceiver] - Patched: {method.DeclaringType?.Name}.{method.Name}");
                }
                
                // Check if InputInterface type exists
                var inputInterfaceType = typeof(InputInterface);
                UniLog.Log($"[VRCFTReceiver] InputInterface type: {inputInterfaceType.FullName}");
                
                // Check InputInterface constructors
                var constructors = inputInterfaceType.GetConstructors();
                UniLog.Log($"[VRCFTReceiver] InputInterface has {constructors.Length} constructors:");
                foreach (var ctor in constructors)
                {
                    var parameters = string.Join(", ", ctor.GetParameters().Select(p => p.ParameterType.Name));
                    UniLog.Log($"[VRCFTReceiver] - Constructor({parameters})");
                }
                
                // Run deep analysis
                UniLog.Log("[VRCFTReceiver] Running detailed InputInterface analysis...");
                AnalyzeInputInterfaceDetailed();
                AnalyzeEngineInitializationDetailed();
                
                // Try direct initialization since InputInterface is already created
                UniLog.Log("[VRCFTReceiver] Attempting direct driver initialization...");
                TryDirectDriverInitialization();
                
                UniLog.Log("[VRCFTReceiver] === DIAGNOSIS: OnEngineInit COMPLETE ===");
            }
            catch (Exception ex)
            {
                UniLog.Error($"[VRCFTReceiver] OnEngineInit failed: {ex}");
                throw;
            }
        }

        [HarmonyPatch(typeof(UserRoot), "OnStart")]
        class VRCFTReceiverPatch
        {
            public static void Postfix(UserRoot __instance)
            {
                UniLog.Log($"[VRCFTReceiver] Starting UserRoot");
                if (__instance.ActiveUser.IsLocalUser && VRCFTDriver != null)
                {
                    // Immediately trigger avatar change
                    UniLog.Log("[VRCFTReceiver] Triggering immediate avatar change");
                    VRCFTDriver.AvatarChange(__instance);
                }
                else
                {
                    UniLog.Warning("[VRCFTReceiver] Driver is not initialized!");
                };
            }
        }
        
        // User.OnAwake doesn't exist in new Resonite, remove this patch
        
        // Manual trigger method for testing
        public static void TriggerAvatarChange()
        {
            if (VRCFTDriver != null)
            {
                UniLog.Log("[VRCFTReceiver] Manual avatar change triggered");
                VRCFTDriver.AvatarChange(null);
            }
            else
            {
                UniLog.Warning("[VRCFTReceiver] Cannot trigger avatar change - driver not initialized");
            }
        }
        // All InputInterface constructor patches removed due to API changes in new Resonite
        
        // RegisterInputDriver patch also removed - using direct initialization only
        
        private static void TryDirectDriverInitialization()
        {
            try
            {
                UniLog.Log("[VRCFTReceiver] Attempting to find existing Engine instance...");
                
                // Try to get the Engine instance from the current world/session
                var world = Engine.Current?.WorldManager?.FocusedWorld;
                var engine = Engine.Current;
                
                UniLog.Log($"[VRCFTReceiver] Engine.Current: {engine != null}");
                UniLog.Log($"[VRCFTReceiver] Engine.Current.InputInterface: {engine?.InputInterface != null}");
                
                if (engine?.InputInterface != null && VRCFTDriver == null)
                {
                    UniLog.Log("[VRCFTReceiver] Found existing InputInterface, initializing driver immediately...");
                    
                    try
                    {
                        UniLog.Log("[VRCFTReceiver] Creating VRCFT driver...");
                        VRCFTDriver = new Driver();
                        engine.InputInterface.RegisterInputDriver(VRCFTDriver);
                        UniLog.Log("[VRCFTReceiver] VRCFT driver initialized successfully via direct method!");
                    }
                    catch (Exception ex)
                    {
                        UniLog.Error($"[VRCFTReceiver] Failed to initialize VRCFT driver directly: {ex}");
                    }
                }
                else
                {
                    UniLog.Warning("[VRCFTReceiver] Could not find Engine.InputInterface for direct initialization");
                }
            }
            catch (Exception ex)
            {
                UniLog.Error($"[VRCFTReceiver] Direct initialization attempt failed: {ex}");
            }
        }
        
        private static void AnalyzeInputInterfaceDetailed()
        {
            try
            {
                var inputInterfaceType = typeof(InputInterface);
                UniLog.Log($"[InputInterfaceAnalyzer] Analyzing InputInterface type: {inputInterfaceType.FullName}");
                UniLog.Log($"[InputInterfaceAnalyzer] Assembly: {inputInterfaceType.Assembly.FullName}");
                UniLog.Log($"[InputInterfaceAnalyzer] Assembly Location: {inputInterfaceType.Assembly.Location}");
                
                // Get all constructors
                var constructors = inputInterfaceType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                UniLog.Log($"[InputInterfaceAnalyzer] Found {constructors.Length} constructors:");
                
                foreach (var constructor in constructors)
                {
                    var parameters = constructor.GetParameters();
                    var paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.FullName} {p.Name}"));
                    UniLog.Log($"[InputInterfaceAnalyzer] Constructor: {constructor.Name}({paramStr})");
                    UniLog.Log($"[InputInterfaceAnalyzer] IsPublic: {constructor.IsPublic}, IsPrivate: {constructor.IsPrivate}");
                }
                
                // Get all methods that might be relevant
                var methods = inputInterfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name.Contains("Register") || m.Name.Contains("Init") || m.Name.Contains("Start"))
                    .ToArray();
                    
                UniLog.Log($"[InputInterfaceAnalyzer] Found {methods.Length} relevant methods:");
                foreach (var method in methods)
                {
                    var parameters = method.GetParameters();
                    var paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.FullName} {p.Name}"));
                    UniLog.Log($"[InputInterfaceAnalyzer] Method: {method.Name}({paramStr}) -> {method.ReturnType.FullName}");
                }
                
                // Check if the current patch target exists
                try
                {
                    var engineType = typeof(Engine);
                    UniLog.Log($"[InputInterfaceAnalyzer] Engine type: {engineType.FullName}");
                    
                    var targetConstructor = inputInterfaceType.GetConstructor(new Type[] { engineType });
                    if (targetConstructor != null)
                    {
                        UniLog.Log($"[InputInterfaceAnalyzer] FOUND target constructor: InputInterface(Engine)");
                    }
                    else
                    {
                        UniLog.Warning($"[InputInterfaceAnalyzer] TARGET CONSTRUCTOR NOT FOUND: InputInterface(Engine)");
                        
                        // Try to find what constructors do exist with Engine parameter
                        var engineConstructors = constructors.Where(c => 
                            c.GetParameters().Any(p => p.ParameterType == engineType || p.ParameterType.Name == "Engine")).ToArray();
                        
                        UniLog.Log($"[InputInterfaceAnalyzer] Found {engineConstructors.Length} constructors with Engine parameter:");
                        foreach (var ctor in engineConstructors)
                        {
                            var parameters = ctor.GetParameters();
                            var paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.FullName} {p.Name}"));
                            UniLog.Log($"[InputInterfaceAnalyzer] Alternative constructor: {ctor.Name}({paramStr})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    UniLog.Error($"[InputInterfaceAnalyzer] Error checking target constructor: {ex}");
                }
                
                // Test Harmony patch information
                try
                {
                    var harmony = new Harmony("test.inputinterface.analyzer");
                    var patches = Harmony.GetAllPatchedMethods();
                    UniLog.Log($"[InputInterfaceAnalyzer] Total patched methods in system: {patches.Count()}");
                    
                    var vrcftPatches = patches.Where(m => 
                        Harmony.GetPatchInfo(m).Owners.Contains("dev.hazre.VRCFTReceiver")).ToArray();
                    UniLog.Log($"[InputInterfaceAnalyzer] VRCFTReceiver patches applied: {vrcftPatches.Length}");
                    
                    foreach (var method in vrcftPatches)
                    {
                        UniLog.Log($"[InputInterfaceAnalyzer] Patched method: {method.DeclaringType?.FullName}.{method.Name}");
                    }
                }
                catch (Exception ex)
                {
                    UniLog.Error($"[InputInterfaceAnalyzer] Error checking Harmony patches: {ex}");
                }
                
            }
            catch (Exception ex)
            {
                UniLog.Error($"[InputInterfaceAnalyzer] Analysis failed: {ex}");
            }
        }
        
        private static void AnalyzeEngineInitializationDetailed()
        {
            try
            {
                var engineType = typeof(Engine);
                UniLog.Log($"[InputInterfaceAnalyzer] Analyzing Engine type: {engineType.FullName}");
                
                // Get all methods that might be initialization points
                var methods = engineType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(m => m.Name.Contains("Init") || m.Name.Contains("Start") || m.Name.Contains("Input"))
                    .ToArray();
                    
                UniLog.Log($"[InputInterfaceAnalyzer] Found {methods.Length} relevant Engine methods:");
                foreach (var method in methods)
                {
                    var parameters = method.GetParameters();
                    var paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    UniLog.Log($"[InputInterfaceAnalyzer] Engine method: {method.Name}({paramStr}) -> {method.ReturnType.Name}");
                }
                
                // Check for InputInterface property or field
                var inputProps = engineType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(p => p.PropertyType == typeof(InputInterface) || p.Name.Contains("Input"))
                    .ToArray();
                    
                UniLog.Log($"[InputInterfaceAnalyzer] Found {inputProps.Length} input-related properties:");
                foreach (var prop in inputProps)
                {
                    UniLog.Log($"[InputInterfaceAnalyzer] Engine property: {prop.PropertyType.Name} {prop.Name}");
                }
                
            }
            catch (Exception ex)
            {
                UniLog.Error($"[InputInterfaceAnalyzer] Engine analysis failed: {ex}");
            }
        }
    }
}
