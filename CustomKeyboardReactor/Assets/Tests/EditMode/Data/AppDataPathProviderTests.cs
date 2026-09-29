using System;
using System.IO;
using NUnit.Framework;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 앱 데이터 경로 계산과 기존 데이터 이관을 검증하는 클래스
    public sealed class AppDataPathProviderTests
    {
        private string _temporaryDirectory; // 테스트 데이터 루트 경로

        // 각 테스트의 격리된 데이터 폴더를 생성하는 함수
        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "CustomKeyboardReactorTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
        }

        // 각 테스트가 만든 데이터 폴더를 정리하는 함수
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, true);
            }
        }

        // Windows 빌드 데이터 폴더에서 실행 파일 옆 UserData 경로를 계산하는지 검증하는 함수
        [Test]
        public void ResolvePortableDataRootPath_BuildDataFolder_ReturnsExecutableSiblingUserData()
        {
            string executableDirectory = Path.Combine(_temporaryDirectory, "Build"); // 실행 파일 폴더 경로
            string applicationDataPath = Path.Combine( // Unity 플레이어 데이터 폴더 경로
                executableDirectory,
                "CustomKeyboardReactor_Data");

            string dataRootPath = AppDataPathProvider.ResolvePortableDataRootPath( // 계산된 앱 데이터 경로
                applicationDataPath);

            Assert.That(
                dataRootPath,
                Is.EqualTo(Path.Combine(executableDirectory, "UserData")));
        }

        // 최초 실행 이관이 제품 데이터만 복사하고 기존 데이터를 보존하는지 검증하는 함수
        [Test]
        public void MigrateLegacyDataIfNeeded_OnFirstPortableRun_CopiesOwnedDataAndPreservesLegacy()
        {
            string legacyRootPath = Path.Combine(_temporaryDirectory, "Legacy"); // 기존 앱 데이터 경로
            string portableRootPath = Path.Combine(_temporaryDirectory, "Build", "UserData"); // 새 앱 데이터 경로
            string legacyImagePath = Path.Combine( // 기존 프리셋 이미지 경로
                legacyRootPath,
                "presets",
                "preset-id",
                "images",
                "image-id.png");
            Directory.CreateDirectory(Path.GetDirectoryName(legacyImagePath));
            File.WriteAllText(Path.Combine(legacyRootPath, "app-data.json"), "primary-data");
            File.WriteAllText(Path.Combine(legacyRootPath, "app-data.backup.json"), "backup-data");
            File.WriteAllBytes(legacyImagePath, new byte[] { 1, 2, 3 });
            File.WriteAllText(Path.Combine(legacyRootPath, "Player.log"), "runtime-log");

            bool migrated = AppDataPathProvider.MigrateLegacyDataIfNeeded( // 기존 데이터 이관 결과
                legacyRootPath,
                portableRootPath);

            Assert.That(migrated, Is.True);
            Assert.That(
                File.ReadAllText(Path.Combine(portableRootPath, "app-data.json")),
                Is.EqualTo("primary-data"));
            Assert.That(
                File.ReadAllText(Path.Combine(portableRootPath, "app-data.backup.json")),
                Is.EqualTo("backup-data"));
            Assert.That(
                File.ReadAllBytes(Path.Combine(
                    portableRootPath,
                    "presets",
                    "preset-id",
                    "images",
                    "image-id.png")),
                Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(File.Exists(Path.Combine(portableRootPath, "Player.log")), Is.False);
            Assert.That(File.Exists(Path.Combine(legacyRootPath, "app-data.json")), Is.True);
            Assert.That(File.Exists(legacyImagePath), Is.True);
        }

        // 새 위치에 데이터가 있으면 기존 데이터로 덮어쓰지 않는지 검증하는 함수
        [Test]
        public void MigrateLegacyDataIfNeeded_WhenPortableDataExists_DoesNotOverwrite()
        {
            string legacyRootPath = Path.Combine(_temporaryDirectory, "Legacy"); // 기존 앱 데이터 경로
            string portableRootPath = Path.Combine(_temporaryDirectory, "Build", "UserData"); // 새 앱 데이터 경로
            Directory.CreateDirectory(legacyRootPath);
            Directory.CreateDirectory(portableRootPath);
            File.WriteAllText(Path.Combine(legacyRootPath, "app-data.json"), "legacy-data");
            File.WriteAllText(Path.Combine(portableRootPath, "app-data.json"), "portable-data");

            bool migrated = AppDataPathProvider.MigrateLegacyDataIfNeeded( // 기존 데이터 이관 결과
                legacyRootPath,
                portableRootPath);

            Assert.That(migrated, Is.False);
            Assert.That(
                File.ReadAllText(Path.Combine(portableRootPath, "app-data.json")),
                Is.EqualTo("portable-data"));
        }
    }
}
