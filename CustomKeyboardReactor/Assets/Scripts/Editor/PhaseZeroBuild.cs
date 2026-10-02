using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CustomKeyboardReactor.Editor.Build
{
    // Windows 플레이어 설정과 빌드를 구성하는 클래스
    public static class PhaseZeroBuild
    {
        public const string OverlayScenePath = "Assets/Scenes/Overlay.unity"; // 오버레이 씬 경로
        public const string WindowsOutputPath = "Builds/Windows/CustomKeyboardReactor.exe"; // Windows 빌드 출력 경로

        private const string SceneDirectory = "Assets/Scenes"; // 오버레이 씬 폴더 경로
        private const string WindowsBuildDirectory = "Builds/Windows"; // Windows 빌드 폴더 경로

        // TMP 필수 리소스를 가져오고 런타임 글꼴 셰이더를 포함하는 함수
        [MenuItem("Custom Keyboard Reactor/Settings/Prepare UI Resources")]
        public static void PrepareUiResources()
        {
            if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                CompleteUiResources();
                return;
            }
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui"); // UGUI 패키지 정보
            AssetDatabase.importPackageCompleted += HandleUiResourcesImported;
            AssetDatabase.ImportPackage(Path.Combine(package.resolvedPath,
                "Package Resources/TMP Essential Resources.unitypackage"), false);
        }

        // TMP 필수 리소스 가져오기 완료를 처리하는 함수
        private static void HandleUiResourcesImported(string packageName)
        {
            if (packageName != "TMP Essential Resources") return;
            AssetDatabase.importPackageCompleted -= HandleUiResourcesImported;
            CompleteUiResources();
        }

        // 동적 TMP 글꼴 셰이더를 빌드에서 보존하는 함수
        private static void CompleteUiResources()
        {
            Shader shader = Shader.Find("TextMeshPro/Mobile/Distance Field"); // 런타임 글꼴 셰이더
            if (shader == null) throw new BuildFailedException("TMP UI shader was not imported.");
            SerializedObject settings = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]); // 그래픽 설정
            SerializedProperty shaders = settings.FindProperty("m_AlwaysIncludedShaders"); // 항상 포함할 셰이더 목록
            bool exists = false; // 글꼴 셰이더 포함 여부
            for (int index = 0; index < shaders.arraySize; index++) // 셰이더 목록 인덱스
                exists |= shaders.GetArrayElementAtIndex(index).objectReferenceValue == shader;
            if (!exists)
            {
                shaders.InsertArrayElementAtIndex(shaders.arraySize);
                shaders.GetArrayElementAtIndex(shaders.arraySize - 1).objectReferenceValue = shader;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // 프로젝트 설정과 오버레이 씬을 구성하는 함수
        [MenuItem("Custom Keyboard Reactor/Phase 0/Configure Baseline")]
        public static void Configure()
        {
            ConfigurePlayerSettings();
            ConfigureOverlayScene();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            Debug.Log("CustomKeyboardReactor phase-zero baseline configured.");
        }

        // Windows 64비트 플레이어를 빌드하는 함수
        [MenuItem("Custom Keyboard Reactor/Phase 0/Build Windows 64-bit")]
        public static void BuildWindows64()
        {
            Configure();
            Directory.CreateDirectory(Path.GetFullPath(WindowsBuildDirectory));

            BuildPlayerOptions buildOptions = new BuildPlayerOptions // Windows 플레이어 빌드 옵션
            {
                scenes = new[] { OverlayScenePath },
                locationPathName = WindowsOutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport buildReport = BuildPipeline.BuildPlayer(buildOptions); // Windows 플레이어 빌드 결과
            if (buildReport.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"Windows build failed with result {buildReport.summary.result} and " +
                    $"{buildReport.summary.totalErrors} error(s).");
            }

            Debug.Log(
                $"Windows build completed: {WindowsOutputPath} " +
                $"({buildReport.summary.totalSize} bytes, {buildReport.summary.totalTime}).");
        }

        // Windows 플레이어 설정을 구성하는 함수
        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.productName = ProductInfo.Name;
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.allowFullscreenSwitch = false;
            PlayerSettings.useFlipModelSwapchain = false;

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D11 });
        }

        // 오버레이 씬과 투명 카메라를 구성하는 함수
        private static void ConfigureOverlayScene()
        {
            Directory.CreateDirectory(Path.GetFullPath(SceneDirectory));

            bool sceneExists = File.Exists(Path.GetFullPath(OverlayScenePath)); // 오버레이 씬 존재 여부
            Scene overlayScene = sceneExists // 설정 대상 오버레이 씬
                ? EditorSceneManager.OpenScene(OverlayScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera overlayCamera = Object.FindAnyObjectByType<Camera>(); // 오버레이 렌더링 카메라
            if (overlayCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera"); // 카메라 게임 오브젝트
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
                overlayCamera = cameraObject.AddComponent<Camera>();
            }

            overlayCamera.clearFlags = CameraClearFlags.SolidColor;
            overlayCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            overlayCamera.orthographic = true;
            overlayCamera.allowHDR = false;
            overlayCamera.allowMSAA = false;

            EditorSceneManager.SaveScene(overlayScene, OverlayScenePath);
        }

        // 플레이어 빌드 대상 씬을 구성하는 함수
        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(OverlayScenePath, true),
            };
        }
    }
}
