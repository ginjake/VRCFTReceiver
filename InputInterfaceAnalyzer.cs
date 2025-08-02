using System;
using System.Reflection;
using System.Linq;
using FrooxEngine;
using Elements.Core;
using HarmonyLib;

namespace VRCFTReceiver
{
    public static class InputInterfaceAnalyzer
    {
        public static void AnalyzeInputInterface()
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
        
        public static void AnalyzeEngineInitialization()
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