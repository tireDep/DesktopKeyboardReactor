using CustomKeyboardReactor.Editor.Build;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 프로젝트 설정과 씬 구성을 검증하는 클래스
    public sealed class ProjectBaselineTests
    {
        // 제품 이름 일치 여부를 검증하는 함수
        [Test]
        public void ProductName_MatchesRuntimeProductInfo()
        {
            Assert.That(PlayerSettings.productName, Is.EqualTo(ProductInfo.Name));
        }

        // Windows 플레이어 설정을 검증하는 함수
        [Test]
        public void WindowsPlayerSettings_MatchOverlayBaseline()
        {
            GraphicsDeviceType[] graphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64); // Windows 그래픽 API 목록

            Assert.That(PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64), Is.False);
            Assert.That(graphicsApis, Is.EqualTo(new[] { GraphicsDeviceType.Direct3D11 }));
            Assert.That(PlayerSettings.runInBackground, Is.True);
            Assert.That(PlayerSettings.fullScreenMode, Is.EqualTo(FullScreenMode.Windowed));
            Assert.That(PlayerSettings.resizableWindow, Is.False);
            Assert.That(PlayerSettings.allowFullscreenSwitch, Is.False);
            Assert.That(PlayerSettings.useFlipModelSwapchain, Is.False);
            Assert.That(QualitySettings.vSyncCount, Is.Zero);
        }

        // 오버레이 렌더링 프레임 제한을 검증하는 함수
        [Test]
        public void OverlayPerformanceSettings_LimitsBackgroundRenderingToSixtyFramesPerSecond()
        {
            int originalTargetFrameRate = Application.targetFrameRate; // 기존 목표 프레임 속도
            int originalVSyncCount = QualitySettings.vSyncCount; // 기존 수직 동기화 간격

            try
            {
                Application.targetFrameRate = -1;
                OverlayPerformanceSettings.Apply();

                Assert.That(Application.targetFrameRate, Is.EqualTo(OverlayPerformanceSettings.TargetFrameRate));
                Assert.That(QualitySettings.vSyncCount, Is.Zero);
            }
            finally
            {
                Application.targetFrameRate = originalTargetFrameRate;
                QualitySettings.vSyncCount = originalVSyncCount;
            }
        }

        // PC 렌더 파이프라인의 불필요한 프레임 텍스처 비활성화를 검증하는 함수
        [Test]
        public void PcRenderPipeline_DisablesUnusedFrameTextures()
        {
            const string pcRenderPipelineAssetPath = "Assets/Settings/PC_RPAsset.asset"; // PC 렌더 파이프라인 에셋 경로
            RenderPipelineAsset renderPipelineAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>( // PC 렌더 파이프라인 에셋
                pcRenderPipelineAssetPath);
            Assert.That(renderPipelineAsset, Is.Not.Null);

            SerializedObject serializedAsset = new SerializedObject(renderPipelineAsset); // 렌더 파이프라인 직렬화 객체
            SerializedProperty depthTextureProperty = serializedAsset.FindProperty("m_RequireDepthTexture"); // 깊이 텍스처 설정
            SerializedProperty opaqueTextureProperty = serializedAsset.FindProperty("m_RequireOpaqueTexture"); // 불투명 텍스처 설정

            Assert.That(depthTextureProperty, Is.Not.Null);
            Assert.That(opaqueTextureProperty, Is.Not.Null);
            Assert.That(depthTextureProperty.boolValue, Is.False);
            Assert.That(opaqueTextureProperty.boolValue, Is.False);
        }

        // 오버레이 씬과 투명 카메라를 검증하는 함수
        [Test]
        public void OverlayScene_IsEnabledAndUsesTransparentCameraClear()
        {
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes; // 플레이어 빌드 대상 씬 목록
            Assert.That(buildScenes, Has.Length.EqualTo(1));
            Assert.That(buildScenes[0].path, Is.EqualTo(PhaseZeroBuild.OverlayScenePath));
            Assert.That(buildScenes[0].enabled, Is.True);

            EditorSceneManager.OpenScene(PhaseZeroBuild.OverlayScenePath, OpenSceneMode.Single);
            Camera overlayCamera = Object.FindAnyObjectByType<Camera>(); // 오버레이 카메라

            Assert.That(overlayCamera, Is.Not.Null);
            Assert.That(overlayCamera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(overlayCamera.backgroundColor.a, Is.EqualTo(0f));
            Assert.That(overlayCamera.orthographic, Is.True);
            Assert.That(overlayCamera.allowHDR, Is.False);
            Assert.That(overlayCamera.allowMSAA, Is.False);
        }
    }
}
