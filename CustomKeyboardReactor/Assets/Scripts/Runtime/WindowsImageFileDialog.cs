using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CustomKeyboardReactor
{
    // Windows 이미지 파일 선택을 인프라 내부에서 처리하는 클래스
    public static class WindowsImageFileDialog
    {
        // PNG 및 JPEG 파일 경로를 선택하는 함수
        public static string[] SelectImages()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            const int bufferCharacters = 65536; // 다중 선택 경로 버퍼 크기
            IntPtr buffer = Marshal.AllocCoTaskMem(bufferCharacters * sizeof(char)); // 선택 경로 버퍼
            try
            {
                Marshal.Copy(new char[bufferCharacters], 0, buffer, bufferCharacters);
                OpenFileName dialog = new OpenFileName // 파일 선택 대화상자 설정
                {
                    StructureSize = Marshal.SizeOf<OpenFileName>(),
                    Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle,
                    Filter = "이미지 (PNG, JPG, JPEG)\0*.png;*.jpg;*.jpeg\0\0",
                    FileBuffer = buffer,
                    MaximumFileCharacters = bufferCharacters,
                    Title = "프리셋 이미지 추가",
                    Flags = 0x00080000 | 0x00001000 | 0x00000200 | 0x00000008,
                };
                if (!GetOpenFileName(ref dialog))
                {
                    if (CommDlgExtendedError() != 0) throw new IOException("이미지 파일 선택 창을 열 수 없습니다.");
                    return Array.Empty<string>();
                }
                string[] parts = Marshal.PtrToStringUni(buffer, bufferCharacters)
                    .Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries); // 선택 경로 구성 요소 목록
                if (parts.Length == 1) return parts;
                string[] paths = new string[parts.Length - 1]; // 선택 파일 경로 목록
                for (int index = 1; index < parts.Length; index++) // 선택 파일 인덱스
                    paths[index - 1] = Path.Combine(parts[0], parts[index]);
                return paths;
            }
            finally
            {
                Marshal.FreeCoTaskMem(buffer);
            }
#elif UNITY_EDITOR
            string selected = UnityEditor.EditorUtility.OpenFilePanelWithFilters(
                "프리셋 이미지 추가", "", new[] { "이미지", "png,jpg,jpeg" }); // Editor 선택 파일 경로
            return string.IsNullOrEmpty(selected) ? Array.Empty<string>() : new[] { selected };
#else
            return Array.Empty<string>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Windows 파일 선택 대화상자 데이터를 보관하는 구조체
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public int StructureSize; // 구조체 크기
            public IntPtr Owner; // 소유 창 핸들
            public IntPtr Instance; // 인스턴스 핸들
            public string Filter; // 파일 확장자 필터
            public IntPtr CustomFilter; // 사용자 필터 버퍼
            public int MaximumCustomFilter; // 사용자 필터 크기
            public int FilterIndex; // 선택 필터 번호
            public IntPtr FileBuffer; // 선택 파일 버퍼
            public int MaximumFileCharacters; // 파일 버퍼 문자 수
            public IntPtr FileTitle; // 파일 이름 버퍼
            public int MaximumFileTitle; // 파일 이름 버퍼 크기
            public string InitialDirectory; // 시작 폴더 경로
            public string Title; // 대화상자 제목
            public int Flags; // 선택 동작 플래그
            public short FileOffset; // 파일 이름 시작 위치
            public short ExtensionOffset; // 확장자 시작 위치
            public string DefaultExtension; // 기본 확장자
            public IntPtr CustomData; // 사용자 데이터
            public IntPtr Hook; // 훅 함수 포인터
            public string TemplateName; // 템플릿 이름
            public IntPtr Reserved; // 예약 포인터
            public int ReservedValue; // 예약 값
            public int ExtendedFlags; // 확장 플래그
        }

        [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetOpenFileName(ref OpenFileName dialog);

        [DllImport("comdlg32.dll")]
        private static extern uint CommDlgExtendedError();
#endif
    }
}
