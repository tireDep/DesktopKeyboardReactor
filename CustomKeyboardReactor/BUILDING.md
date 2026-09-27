# 빌드 및 검증

이 프로젝트의 기준 Unity 버전은 `6000.5.6f1`이며 Windows 64비트 플레이어를 대상으로 한다.

## 0단계 기준 설정

- 그래픽 API 자동 선택을 끄고 Direct3D 11만 사용한다.
- DWM 투명화를 위해 DXGI Flip Model Swapchain을 끄고 D3D11 BitBlt 모델을 사용한다.
- 플레이어는 창 모드로 시작하며 전체 화면 전환과 창 크기 조절을 끈다.
- 포커스를 잃어도 실행을 계속한다.
- 백그라운드 렌더링은 VSync 없이 초당 60프레임으로 제한한다.
- PC 렌더 파이프라인의 Depth Texture와 Opaque Texture를 사용하지 않는다.
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

## 2단계 전역 입력 공급자

- `ActivityInputController`가 Windows 플레이어에서 키보드와 마우스 저수준 훅을 시작하고 종료 시 해제한다.
- 훅 콜백은 키 종류와 문자열을 전달하지 않고 반응 입력 발생 여부와 마우스 화면 좌표만 스레드 안전 큐에 기록한다.
- `ActivityInputCoordinator`는 메인 스레드에서 큐를 비우며 장치별 활성화, 설정 상태와 자체 UI 입력 제외 판정을 적용한다.
- 전체 입력 수는 `ActivityInputController.TotalInputCount`에서 확인한다.

## 2단계 수동 검증 준비

1. Unity Build Profiles에서 Windows 플랫폼의 `Development Build`와 `Script Debugging`을 켜고 플레이어를 빌드한다.
2. 빌드한 플레이어를 실행하고 IDE 디버거를 Unity Player 프로세스에 연결한다.
3. `ActivityInputController.TotalInputCount`를 조사식 또는 Watch에 등록한다.
4. 입력 지연을 비교할 수 있는 텍스트 편집기와 X1/X2 버튼을 확인할 수 있는 앱을 함께 준비한다.

## 2단계 수동 검증

1. 텍스트 편집기에 포커스를 둔 상태에서 문자키, 숫자키, 방향키, 수정키와 기능키를 한 번씩 눌러 입력마다 전체 입력 수가 1씩 증가하는지 확인한다.
2. 키 하나를 계속 눌러 자동 반복이 발생해도 키를 놓기 전에는 전체 입력 수가 추가로 증가하지 않는지 확인한다.
3. 키를 놓는 동작만으로 전체 입력 수가 증가하지 않는지 확인한다.
4. 왼쪽, 오른쪽, 가운데, X1과 X2 버튼을 눌러 각각 전체 입력 수가 1씩 증가하는지 확인한다.
5. 마우스 이동, 휠 회전과 버튼 해제만으로 전체 입력 수가 증가하지 않는지 확인한다.
6. `KeyboardReactionEnabled`와 `MouseButtonReactionEnabled`를 각각 끈 동안 해당 장치 입력이 집계되지 않고, 다시 켠 뒤 과거 입력이 뒤늦게 집계되지 않는지 확인한다.
7. `IsConfiguring`을 켠 동안 모든 입력이 집계되지 않고, 끈 뒤 과거 입력이 뒤늦게 집계되지 않는지 확인한다.
8. Windows `SendInput` 기반 자동화 도구로 생성한 키보드와 마우스 입력이 전체 입력 수에 포함되지 않는지 확인한다.
9. 입력을 빠르게 반복하면서 텍스트 편집기의 입력 반응과 포인터 동작에 체감 지연이 없는지 확인한다.
10. 플레이어를 정상 종료한 뒤 로그에 훅 시작 실패, 훅 해제 실패 또는 처리되지 않은 예외가 없는지 확인한다.

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
