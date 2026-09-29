using System;
using System.IO;
using UnityEngine;

namespace CustomKeyboardReactor
{
    // 실행 환경에 맞는 앱 데이터 루트 경로를 준비하는 클래스
    public static class AppDataPathProvider
    {
        private const string PortableDataDirectoryName = "UserData"; // 휴대용 앱 데이터 폴더 이름
        private const string PrimaryDataFileName = "app-data.json"; // 주 앱 데이터 파일 이름
        private const string BackupDataFileName = "app-data.backup.json"; // 백업 앱 데이터 파일 이름
        private const string PresetsDirectoryName = "presets"; // 프리셋 폴더 이름
        private const string DraftsDirectoryName = "drafts"; // 편집 초안 폴더 이름
        private const string MigrationDirectoryPrefix = ".UserData-migration-"; // 이관 임시 폴더 접두사

        private static readonly object PreparationLock = new object(); // 현재 실행 경로 준비 잠금
        private static string _preparedDataRootPath; // 준비된 현재 앱 데이터 경로

        // 현재 실행 환경의 앱 데이터 루트 경로를 준비하는 함수
        public static string PrepareCurrentDataRootPath()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            lock (PreparationLock)
            {
                if (!string.IsNullOrEmpty(_preparedDataRootPath))
                {
                    return _preparedDataRootPath;
                }

                string portableRootPath = ResolvePortableDataRootPath(Application.dataPath); // 휴대용 앱 데이터 경로
                MigrateLegacyDataIfNeeded(Application.persistentDataPath, portableRootPath);
                _preparedDataRootPath = portableRootPath;
                return _preparedDataRootPath;
            }
#else
            return Application.persistentDataPath;
#endif
        }

        // Unity 플레이어 데이터 폴더에서 실행 파일 옆 UserData 경로를 계산하는 함수
        public static string ResolvePortableDataRootPath(string applicationDataPath)
        {
            string normalizedApplicationDataPath = NormalizeRootPath( // 정규화된 플레이어 데이터 경로
                applicationDataPath,
                nameof(applicationDataPath));
            DirectoryInfo applicationDataDirectory = new DirectoryInfo( // 플레이어 데이터 폴더 정보
                normalizedApplicationDataPath);
            DirectoryInfo executableDirectory = applicationDataDirectory.Parent; // 실행 파일 폴더 정보
            if (executableDirectory == null)
            {
                throw new InvalidOperationException("Executable directory could not be resolved.");
            }

            return Path.Combine(executableDirectory.FullName, PortableDataDirectoryName);
        }

        // 새 데이터가 없을 때 기존 LocalLow 제품 데이터를 원본 보존 방식으로 이관하는 함수
        public static bool MigrateLegacyDataIfNeeded(
            string legacyRootPath,
            string portableRootPath)
        {
            string normalizedLegacyRootPath = NormalizeRootPath( // 정규화된 기존 데이터 경로
                legacyRootPath,
                nameof(legacyRootPath));
            string normalizedPortableRootPath = NormalizeRootPath( // 정규화된 휴대용 데이터 경로
                portableRootPath,
                nameof(portableRootPath));
            if (string.Equals(
                    normalizedLegacyRootPath,
                    normalizedPortableRootPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string legacyDataFilePath = Path.Combine( // 기존 주 데이터 파일 경로
                normalizedLegacyRootPath,
                PrimaryDataFileName);
            if (!File.Exists(legacyDataFilePath) ||
                Directory.Exists(normalizedPortableRootPath))
            {
                return false;
            }

            DirectoryInfo portableParentDirectory = Directory.GetParent( // 휴대용 데이터 상위 폴더 정보
                normalizedPortableRootPath);
            if (portableParentDirectory == null)
            {
                throw new InvalidOperationException("Portable data parent directory could not be resolved.");
            }

            Directory.CreateDirectory(portableParentDirectory.FullName);
            string migrationDirectoryPath = Path.Combine( // 이관 임시 폴더 경로
                portableParentDirectory.FullName,
                MigrationDirectoryPrefix + Guid.NewGuid().ToString("N"));
            bool migrationCompleted = false; // 이관 완료 여부
            try
            {
                Directory.CreateDirectory(migrationDirectoryPath);
                CopyFileIfExists(
                    legacyDataFilePath,
                    Path.Combine(migrationDirectoryPath, PrimaryDataFileName));
                CopyFileIfExists(
                    Path.Combine(normalizedLegacyRootPath, BackupDataFileName),
                    Path.Combine(migrationDirectoryPath, BackupDataFileName));
                CopyDirectoryIfExists(
                    Path.Combine(normalizedLegacyRootPath, PresetsDirectoryName),
                    Path.Combine(migrationDirectoryPath, PresetsDirectoryName));
                CopyDirectoryIfExists(
                    Path.Combine(normalizedLegacyRootPath, DraftsDirectoryName),
                    Path.Combine(migrationDirectoryPath, DraftsDirectoryName));

                if (Directory.Exists(normalizedPortableRootPath))
                {
                    return false;
                }

                Directory.Move(migrationDirectoryPath, normalizedPortableRootPath);
                migrationCompleted = true;
                return true;
            }
            finally
            {
                if (!migrationCompleted && Directory.Exists(migrationDirectoryPath))
                {
                    Directory.Delete(migrationDirectoryPath, true);
                }
            }
        }

        // 선택한 파일이 있으면 대상 경로로 복사하는 함수
        private static void CopyFileIfExists(string sourcePath, string destinationPath)
        {
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, destinationPath, false);
            }
        }

        // 선택한 폴더가 있으면 하위 구조와 파일을 대상 경로로 복사하는 함수
        private static void CopyDirectoryIfExists(
            string sourceDirectoryPath,
            string destinationDirectoryPath)
        {
            if (!Directory.Exists(sourceDirectoryPath))
            {
                return;
            }

            Directory.CreateDirectory(destinationDirectoryPath);
            foreach (string sourceSubdirectoryPath in Directory.GetDirectories( // 복사할 하위 폴더 경로
                         sourceDirectoryPath,
                         "*",
                         SearchOption.AllDirectories))
            {
                string relativeDirectoryPath = sourceSubdirectoryPath.Substring( // 원본 기준 하위 폴더 경로
                    sourceDirectoryPath.Length).TrimStart(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(
                    destinationDirectoryPath,
                    relativeDirectoryPath));
            }

            foreach (string sourceFilePath in Directory.GetFiles( // 복사할 하위 파일 경로
                         sourceDirectoryPath,
                         "*",
                         SearchOption.AllDirectories))
            {
                string relativeFilePath = sourceFilePath.Substring( // 원본 기준 하위 파일 경로
                    sourceDirectoryPath.Length).TrimStart(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                string destinationFilePath = Path.Combine( // 복사 대상 파일 경로
                    destinationDirectoryPath,
                    relativeFilePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath));
                File.Copy(sourceFilePath, destinationFilePath, false);
            }
        }

        // 필수 루트 경로를 절대 경로로 정규화하는 함수
        private static string NormalizeRootPath(string rootPath, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("Data root path is required.", parameterName);
            }

            return Path.GetFullPath(rootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
