using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CustomKeyboardReactor
{
    // Unity 포커스 중 물리 키 눌림을 수집하는 클래스
    internal sealed class UnityFocusedKeyboardInputSource : IActivityInputSource
    {
        private readonly Queue<ActivityInputEvent> _pendingInputs = new Queue<ActivityInputEvent>(); // 대기 중인 키보드 반응 입력 큐
        private int _lastCapturedFrame = -1; // 마지막 키보드 입력 확인 프레임

        public bool IsRunning { get; private set; } // 포커스 키보드 공급자 실행 여부

        // 포커스 키보드 입력 수집을 시작하는 함수
        public bool TryStart()
        {
            IsRunning = true;
            return true;
        }

        // 포커스 키보드 입력 수집을 정지하는 함수
        public void Stop()
        {
            _pendingInputs.Clear();
            _lastCapturedFrame = -1;
            IsRunning = false;
        }

        // 현재 프레임의 다음 키보드 반응 입력을 반환하는 함수
        public bool TryDequeue(out ActivityInputEvent inputEvent)
        {
            CaptureCurrentFrameInputs();
            if (_pendingInputs.Count == 0)
            {
                inputEvent = default;
                return false;
            }

            inputEvent = _pendingInputs.Dequeue();
            return true;
        }

        // 포커스 키보드 입력 자원을 정리하는 함수
        public void Dispose()
        {
            Stop();
        }

        // 현재 프레임에서 새로 눌린 물리 키를 큐에 추가하는 함수
        private void CaptureCurrentFrameInputs()
        {
            if (!IsRunning || !Application.isFocused)
            {
                _pendingInputs.Clear();
                return;
            }

            int currentFrame = Time.frameCount; // 현재 Unity 프레임
            if (_lastCapturedFrame == currentFrame)
            {
                return;
            }

            _lastCapturedFrame = currentFrame;
            Keyboard keyboard = Keyboard.current; // 현재 물리 키보드
            if (keyboard == null)
            {
                return;
            }

            foreach (KeyControl keyControl in keyboard.allKeys) // 물리 키 제어 목록
            {
                if (!keyControl.wasPressedThisFrame)
                {
                    continue;
                }

                _pendingInputs.Enqueue(ActivityInputEvent.CreateKeyboard());
            }
        }
    }
}
