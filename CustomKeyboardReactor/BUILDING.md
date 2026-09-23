# 빌드 및 검증

이 프로젝트의 기준 Unity 버전은 `6000.5.6f1`이며 Windows 64비트 플레이어를 대상으로 한다.

## 0단계 기준 설정

- 그래픽 API 자동 선택을 끄고 Direct3D 11만 사용한다.
- DWM 투명화를 위해 DXGI Flip Model Swapchain을 끄고 D3D11 BitBlt 모델을 사용한다.
- 플레이어는 창 모드로 시작하며 전체 화면 전환과 창 크기 조절을 끈다.
- 포커스를 잃어도 실행을 계속한다.
- `Assets/Scenes/Overlay.unity`의 카메라는 알파 0으로 화면을 지운다.
- Edit Mode 테스트 결과는 `TestResults/EditMode.xml`에 기록한다.
- Windows 빌드는 `Builds/Windows/CustomKeyboardReactor.exe`에 생성한다.

## 1단계 투명 오버레이

- `OverlayWindowService`가 플레이어 창 핸들을 지연 획득한 뒤 DWM 투명화, 보더리스, 항상 위와 클릭 통과를 적용한다.
- 포커스와 디스플레이 구성이 바뀌면 창 속성을 다시 적용한다.
- 종료할 때 초기 창 스타일과 항상 위 상태를 복원한다.
- 런타임에 중앙의 청록색 불투명 이미지를 생성해 투명 배경과 렌더링 결과를 구분한다.
- 불투명 검증 이미지 위에서는 창이 클릭을 받고 나머지 영역에서는 클릭이 아래 창으로 통과한다.
- 실제 캐릭터 이미지의 알파 기반 클릭 판정은 개발 순서 5단계에서 구현한다.

## Unity Editor 절차

1. `Custom Keyboard Reactor > Phase 0 > Configure Baseline`을 실행한다.
2. Unity Test Runner에서 Edit Mode 테스트를 실행한다.
3. `Custom Keyboard Reactor > Phase 0 > Build Windows 64-bit`을 실행한다.
4. `Builds/Windows/CustomKeyboardReactor.exe`를 실행해 아래 1단계 수동 검증을 진행한다.

## 1단계 수동 검증

1. 바탕 화면에 클릭 결과를 확인할 수 있는 앱을 열어 둔다.
2. 빌드를 실행하고 창 테두리와 불투명 배경 없이 중앙의 청록색 이미지만 보이는지 확인한다.
3. 다른 창을 활성화해도 오버레이 이미지가 위에 유지되는지 확인한다.
4. 오버레이의 투명 영역을 클릭했을 때 아래 앱이 입력을 받는지 확인한다.
5. 청록색 이미지 위를 클릭했을 때 아래 앱이 입력을 받지 않는지 확인한다.
6. 작업 표시줄에서 오버레이를 다시 활성화한 뒤 투명화와 항상 위 상태가 유지되는지 확인한다.
7. 앱을 정상 종료하고 Unity 로그에 처리되지 않은 예외가 없는지 확인한다.

## 명령줄 절차

저장소 루트의 PowerShell에서 다음 명령을 실행한다. Unity 설치 경로가 다르면 `$unityEditor` 값만 바꾼다.

```powershell
$unityEditor = "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe" # Unity Editor 실행 파일 경로
$projectPath = Join-Path $PWD "CustomKeyboardReactor" # Unity 프로젝트 루트 경로

$buildArguments = @( # Windows 빌드 인자
  "-batchmode",
  "-quit",
  "-projectPath", "`"$projectPath`"",
  "-executeMethod", "CustomKeyboardReactor.Editor.Build.PhaseZeroBuild.BuildWindows64",
  "-logFile", "`"$projectPath\Logs\PhaseZeroBuild.log`""
)
$buildProcess = Start-Process -FilePath $unityEditor -ArgumentList $buildArguments -WindowStyle Hidden -Wait -PassThru # 동기 빌드 프로세스
if ($buildProcess.ExitCode -ne 0) { throw "Windows build failed with exit code $($buildProcess.ExitCode)." }

New-Item -ItemType Directory -Path "$projectPath\TestResults" -Force | Out-Null
$testArguments = @( # Edit Mode 테스트 인자
  "-batchmode",
  "-projectPath", "`"$projectPath`"",
  "-runTests",
  "-testPlatform", "EditMode",
  "-testResults", "`"$projectPath\TestResults\EditMode.xml`"",
  "-logFile", "`"$projectPath\Logs\EditModeTests.log`""
)
$testProcess = Start-Process -FilePath $unityEditor -ArgumentList $testArguments -WindowStyle Hidden -Wait -PassThru # 동기 테스트 프로세스
if ($testProcess.ExitCode -ne 0) { throw "Edit Mode tests failed with exit code $($testProcess.ExitCode)." }
```

두 명령의 종료 코드가 `0`이어야 하며, Unity Console과 로그에 컴파일 오류 또는 처리되지 않은 예외가 없어야 한다.
