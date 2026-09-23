using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace TicTacFade.EditorTools
{
    /// <summary>
    /// One-shot, reproducible Android player configuration for the first
    /// build, so it never depends on someone remembering which Inspector
    /// fields to touch. Usage: menu "Tic-Tac-Fade > Configure Android Player Settings".
    /// Does not touch keystore/signing — that stays a manual, credential-sensitive step.
    /// </summary>
    public static class AndroidBuildConfigurator
    {
        const string CompanyName = "HeuMila";
        const string ProductName = "Tic-Tac-Fade";
        const string ApplicationIdentifier = "com.heumila.tictacfade";
        const string BundleVersion = "0.1.0";
        const int BundleVersionCode = 1;

        // GDD/task ask for API level 23 or higher. AndroidApiLevel23 (and
        // every level down to 16) is marked obsolete in this Unity version,
        // so 26 is the lowest level that still satisfies ">=23" without
        // using a deprecated enum value.
        const AndroidSdkVersions MinSdkVersion = AndroidSdkVersions.AndroidApiLevel26;

        [MenuItem("Tic-Tac-Fade/Configure Android Player Settings")]
        public static void ConfigureForAndroid()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationIdentifier);

            PlayerSettings.bundleVersion = BundleVersion;
            PlayerSettings.Android.bundleVersionCode = BundleVersionCode;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = MinSdkVersion;

            // GDD §2.3: Android, portrait. Re-asserted here (not just relying
            // on a prior one-off change) so this method is the single source
            // of truth for "is the project configured for Android".
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            AssetDatabase.SaveAssets();

            int sceneCount = EditorBuildSettings.scenes.Length;
            if (sceneCount != 1)
            {
                Debug.LogWarning($"Tic-Tac-Fade: Build Settings tiene {sceneCount} escena(s), se esperaba 1. Revisar antes de buildear.");
            }

            Debug.Log($"Tic-Tac-Fade: Android player configurado — {ApplicationIdentifier} v{BundleVersion} " +
                $"(code {BundleVersionCode}), IL2CPP/ARM64, minSdk {MinSdkVersion}, portrait, " +
                $"{sceneCount} escena(s) en Build Settings. Falta el keystore de firma, que queda a mano.");
        }
    }
}
