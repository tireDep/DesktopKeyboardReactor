using System;

namespace CustomKeyboardReactor
{
    // 반응 입력 공급자의 공통 경계를 정의하는 인터페이스
    public interface IActivityInputSource : IDisposable
    {
        bool IsRunning { get; } // 공급자 실행 여부

        // 반응 입력 수집을 시작하는 함수
        bool TryStart();

        // 반응 입력 수집을 중지하는 함수
        void Stop();

        // 대기 중인 반응 입력을 반환하는 함수
        bool TryDequeue(out ActivityInputEvent inputEvent);
    }
}
