using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using OrbitalPanopticon.UI;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.Editor
{
    [InitializeOnLoad]
    public static class ForceCompileScripts
    {
        static ForceCompileScripts()
        {
            EditorApplication.delayCall += () =>
            {
                Debug.Log("<color=#33FF88>[Compilation] Scripts compiled and domain reloaded successfully!</color>");
            };
        }

        [MenuItem("Tools/Force Recompile All Scripts %#r")]
        public static void Recompile()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
            Debug.Log("<color=#33FF88>[Compilation] Force script compilation requested via CompilationPipeline.</color>");
        }

        [MenuItem("Tools/Setup Dual Volume Sliders In Current Scene")]
        public static void SetupDualVolumeSlidersInScene()
        {
            var menuController = Object.FindAnyObjectByType<MainMenuUIController>();
            if (menuController != null)
            {
                menuController.EnsureVolumeSliders();
                EditorUtility.SetDirty(menuController);
                EditorSceneManager.MarkSceneDirty(menuController.gameObject.scene);
                Debug.Log("<color=#33FF88>[Setup] Dual Volume Sliders configured in Main Menu scene!</color>");
            }

            var pauseController = Object.FindAnyObjectByType<PauseMenuController>();
            if (pauseController != null)
            {
                pauseController.EnsureSettingsSubPanelControls();
                EditorUtility.SetDirty(pauseController);
                EditorSceneManager.MarkSceneDirty(pauseController.gameObject.scene);
                Debug.Log("<color=#33FF88>[Setup] Dual Volume Sliders configured in In-Game Pause Menu scene!</color>");
            }
        }
    }
}
