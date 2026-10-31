#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace OrbitalPanopticon.Core.Editor
{
    /// <summary>
    /// Checks if the official FMOD Unity Integration package (v2.03.08) is installed.
    /// Automatically manages the 'FMOD_UNITY' scripting define symbol so fallback stubs
    /// yield seamlessly when the actual FMOD package is imported.
    /// </summary>
    [InitializeOnLoad]
    public static class FMODCompatibilityChecker
    {
        private const string FmodDefineSymbol = "FMOD_UNITY";

        static FMODCompatibilityChecker()
        {
            EditorApplication.delayCall += CheckAndConfigureFMODDefine;
        }

        [MenuItem("Tools/Orbital Panopticon/Check FMOD Integration")]
        public static void CheckAndConfigureFMODDefine()
        {
            NamedBuildTarget buildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string defines = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
            var defineList = defines.Split(';').Select(d => d.Trim()).Where(d => !string.IsNullOrEmpty(d)).ToList();

            bool hasRealFMOD = HasOfficialFMODAssembly();
            bool hasDefine = defineList.Contains(FmodDefineSymbol);

            if (hasRealFMOD && !hasDefine)
            {
                defineList.Add(FmodDefineSymbol);
                PlayerSettings.SetScriptingDefineSymbols(buildTarget, string.Join(";", defineList));
                Debug.Log($"<color=#00FFCC>[FMODCompatibilityChecker]</color> Official FMOD integration detected. Added define symbol '{FmodDefineSymbol}'.");
            }
            else if (!hasRealFMOD && hasDefine)
            {
                defineList.Remove(FmodDefineSymbol);
                PlayerSettings.SetScriptingDefineSymbols(buildTarget, string.Join(";", defineList));
                Debug.Log($"<color=#FFCC00>[FMODCompatibilityChecker]</color> FMOD integration not found. Removed define symbol '{FmodDefineSymbol}'. Using stub fallback layer.");
            }
        }

        private static bool HasOfficialFMODAssembly()
        {
            // Look for official FMOD assemblies or types that only exist in the official package
            return AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
            {
                string asmName = assembly.GetName().Name;
                return (asmName == "FMODUnity" || asmName == "FMODUnityResonance") &&
                       assembly.GetType("FMODUnity.Settings") != null;
            });
        }
    }
}
#endif
