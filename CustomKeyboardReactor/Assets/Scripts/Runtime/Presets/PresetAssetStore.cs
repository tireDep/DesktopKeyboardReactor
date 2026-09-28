using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomKeyboardReactor
{
    // 프리셋 소유 이미지의 저장과 런타임 로드를 관리하는 클래스
    public sealed class PresetAssetStore
    {
        private const string PresetsDirectoryName = "presets"; // 프리셋 루트 폴더 이름
        private const string DraftsDirectoryName = "drafts"; // 편집 초안 루트 폴더 이름
        private const string ImagesDirectoryName = "images"; // 이미지 폴더 이름
        private const string DefaultPresetName = "defaultPreset"; // 기본 프리셋 이름
        private const int DefaultImageSize = 64; // 기본 이미지 한 변 크기

        private readonly string _dataRootPath; // 앱 데이터 루트 경로

        // 앱 데이터 루트 경로를 설정하는 생성자
        public PresetAssetStore(string dataRootPath)
        {
            if (string.IsNullOrWhiteSpace(dataRootPath))
            {
                throw new ArgumentException("Data root path is required.", nameof(dataRootPath));
            }

            _dataRootPath = Path.GetFullPath(dataRootPath);
        }

        // 변경 불가능한 원본에서 편집 가능한 기본 프리셋을 생성하는 함수
        public PresetData CreateDefaultPreset()
        {
            string presetId = CreateInternalId(); // 새 기본 프리셋 ID
            string imageId = CreateInternalId(); // 새 기본 이미지 ID
            string relativePath = BuildImageRelativePath(presetId, imageId, ".png"); // 기본 이미지 상대 경로
            string absolutePath = ResolveAbsolutePath(relativePath); // 기본 이미지 절대 경로
            byte[] imageBytes = CreateDefaultImageBytes(); // 기본 이미지 PNG 데이터

            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllBytes(absolutePath, imageBytes);

            PresetData preset = new PresetData // 새 기본 프리셋 데이터
            {
                Id = presetId,
                Name = DefaultPresetName,
            };
            preset.NormalImages.Add(new ImageAssetData
            {
                Id = imageId,
                RelativePath = relativePath,
                OriginalWidth = DefaultImageSize,
                OriginalHeight = DefaultImageSize,
            });
            return preset;
        }

        // 프리셋 ID가 안전한 경로 조각인지 검증하는 함수
        internal static void ValidatePresetId(string presetId)
        {
            ValidatePathSegment(presetId, nameof(presetId));
        }

        // 외부 이미지를 프리셋 편집 초안 폴더로 가져오는 함수
        public ImageAssetData Import(string presetId, string sourcePath)
        {
            ValidatePathSegment(presetId, nameof(presetId));
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Source image path is required.", nameof(sourcePath));
            }

            string sourceAbsolutePath = Path.GetFullPath(sourcePath); // 가져올 이미지 절대 경로
            if (!File.Exists(sourceAbsolutePath))
            {
                throw new FileNotFoundException("Source image file was not found.", sourceAbsolutePath);
            }

            string extension = NormalizeSupportedExtension(Path.GetExtension(sourceAbsolutePath)); // 이미지 확장자
            byte[] imageBytes = File.ReadAllBytes(sourceAbsolutePath); // 원본 이미지 데이터
            Vector2Int imageSize = ReadImageSize(imageBytes); // 원본 이미지 크기
            string imageId = CreateInternalId(); // 새 이미지 내부 ID
            string relativePath = BuildDraftImageRelativePath(presetId, imageId, extension); // 초안 이미지 상대 경로
            string destinationPath = ResolveAbsolutePath(relativePath); // 저장 이미지 절대 경로

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            File.Copy(sourceAbsolutePath, destinationPath, false);

            return new ImageAssetData
            {
                Id = imageId,
                RelativePath = relativePath,
                OriginalWidth = imageSize.x,
                OriginalHeight = imageSize.y,
            };
        }

        // 원본 프리셋 이미지를 대상 프리셋 폴더에 독립 복사하는 함수
        public void DuplicatePresetAssets(PresetData sourcePreset, PresetData targetPreset)
        {
            if (sourcePreset == null)
            {
                throw new ArgumentNullException(nameof(sourcePreset));
            }

            if (targetPreset == null)
            {
                throw new ArgumentNullException(nameof(targetPreset));
            }

            ValidatePathSegment(sourcePreset.Id, nameof(sourcePreset));
            ValidatePathSegment(targetPreset.Id, nameof(targetPreset));
            targetPreset.NormalImages = new List<ImageAssetData>();
            targetPreset.IdleImages = new List<ImageAssetData>();

            try
            {
                CopyImages(sourcePreset.NormalImages, sourcePreset.Id, targetPreset.Id, targetPreset.NormalImages);
                CopyImages(sourcePreset.IdleImages, sourcePreset.Id, targetPreset.Id, targetPreset.IdleImages);
            }
            catch
            {
                DeletePresetAssets(targetPreset.Id);
                throw;
            }
        }

        // 프리셋이 소유한 이미지 폴더를 삭제하는 함수
        public void DeletePresetAssets(string presetId)
        {
            string presetDirectory = GetPresetDirectory(presetId); // 삭제할 프리셋 폴더 경로
            if (Directory.Exists(presetDirectory))
            {
                Directory.Delete(presetDirectory, true);
            }
        }

        // 편집 초안이 소유한 임시 이미지 폴더를 삭제하는 함수
        public void DiscardDraftAssets(string presetId)
        {
            string draftDirectory = GetDraftDirectory(presetId); // 삭제할 편집 초안 폴더 경로
            if (Directory.Exists(draftDirectory))
            {
                Directory.Delete(draftDirectory, true);
            }
        }

        // 저장된 이미지를 CPU에서 읽을 수 있는 런타임 텍스처로 불러오는 함수
        public Texture2D LoadTexture(ImageAssetData image)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            NormalizeSupportedExtension(Path.GetExtension(image.RelativePath));
            string absolutePath = ResolveAbsolutePath(image.RelativePath); // 불러올 이미지 절대 경로
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException("Preset image file was not found.", absolutePath);
            }

            byte[] imageBytes = File.ReadAllBytes(absolutePath); // 저장된 이미지 데이터
            Texture2D texture = CreateReadableTexture(imageBytes); // 읽기 가능한 런타임 텍스처
            texture.name = image.Id;
            return texture;
        }

        // 저장된 이미지를 정상적으로 불러올 수 있는지 확인하는 함수
        public bool CanLoad(ImageAssetData image)
        {
            Texture2D texture = null; // 검증용 런타임 텍스처
            try
            {
                texture = LoadTexture(image);
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidDataException ||
                exception is NotSupportedException)
            {
                return false;
            }
            finally
            {
                DestroyTexture(texture);
            }
        }

        // 이미지가 지정한 프리셋 소유 폴더에 있고 정상적으로 로드되는지 확인하는 함수
        public bool CanLoadOwnedImage(string presetId, ImageAssetData image)
        {
            return HasExpectedImagePath(presetId, image, false) && CanLoad(image);
        }

        // 적용할 프리셋의 초안 이미지를 소유 폴더로 복사하는 함수
        internal DraftAssetCommit PrepareDraftAssets(PresetData preset)
        {
            if (preset == null)
            {
                throw new ArgumentNullException(nameof(preset));
            }

            ValidatePathSegment(preset.Id, nameof(preset));
            DraftAssetCommit commit = new DraftAssetCommit(this, preset.Id); // 초안 이미지 적용 작업
            try
            {
                PrepareDraftImages(preset.Id, preset.NormalImages, commit);
                PrepareDraftImages(preset.Id, preset.IdleImages, commit);
                return commit;
            }
            catch
            {
                commit.Dispose();
                throw;
            }
        }

        // 이미지 목록을 새 프리셋 폴더로 복사하는 함수
        private void CopyImages(
            IReadOnlyList<ImageAssetData> sourceImages,
            string sourcePresetId,
            string targetPresetId,
            ICollection<ImageAssetData> targetImages)
        {
            if (sourceImages == null)
            {
                return;
            }

            foreach (ImageAssetData sourceImage in sourceImages)
            {
                if (!HasExpectedImagePath(sourcePresetId, sourceImage, false))
                {
                    throw new InvalidDataException("Preset contains an image owned by another preset.");
                }

                string sourcePath = ResolveAbsolutePath(sourceImage.RelativePath); // 원본 이미지 절대 경로
                if (!File.Exists(sourcePath))
                {
                    throw new FileNotFoundException("Preset image file was not found.", sourcePath);
                }

                string extension = NormalizeSupportedExtension(Path.GetExtension(sourceImage.RelativePath)); // 복사 이미지 확장자
                string targetImageId = CreateInternalId(); // 복사 이미지 내부 ID
                string targetRelativePath = BuildImageRelativePath( // 복사 이미지 상대 경로
                    targetPresetId,
                    targetImageId,
                    extension);
                string targetPath = ResolveAbsolutePath(targetRelativePath); // 복사 이미지 절대 경로
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                File.Copy(sourcePath, targetPath, false);
                targetImages.Add(new ImageAssetData
                {
                    Id = targetImageId,
                    RelativePath = targetRelativePath,
                    OriginalWidth = sourceImage.OriginalWidth,
                    OriginalHeight = sourceImage.OriginalHeight,
                });
            }
        }

        // 초안 이미지 목록의 임시 파일을 프리셋 소유 폴더로 복사하는 함수
        private void PrepareDraftImages(
            string presetId,
            IReadOnlyList<ImageAssetData> images,
            DraftAssetCommit commit)
        {
            if (images == null)
            {
                return;
            }

            foreach (ImageAssetData image in images)
            {
                if (HasExpectedImagePath(presetId, image, false))
                {
                    continue;
                }

                if (!HasExpectedImagePath(presetId, image, true))
                {
                    continue;
                }

                string draftPath = ResolveAbsolutePath(image.RelativePath); // 초안 이미지 절대 경로
                if (!File.Exists(draftPath))
                {
                    throw new FileNotFoundException("Draft preset image file was not found.", draftPath);
                }

                string extension = NormalizeSupportedExtension(Path.GetExtension(image.RelativePath)); // 확정 이미지 확장자
                string ownedRelativePath = BuildImageRelativePath(presetId, image.Id, extension); // 확정 이미지 상대 경로
                string ownedPath = ResolveAbsolutePath(ownedRelativePath); // 확정 이미지 절대 경로
                Directory.CreateDirectory(Path.GetDirectoryName(ownedPath));
                File.Copy(draftPath, ownedPath, false);
                commit.Record(image, image.RelativePath, ownedPath);
                image.RelativePath = ownedRelativePath;
            }
        }

        // 이미지 참조가 프리셋의 최종 또는 초안 폴더 형식과 일치하는지 확인하는 함수
        private static bool HasExpectedImagePath(
            string presetId,
            ImageAssetData image,
            bool isDraft)
        {
            if (image == null ||
                string.IsNullOrWhiteSpace(image.Id) ||
                string.IsNullOrWhiteSpace(image.RelativePath))
            {
                return false;
            }

            try
            {
                ValidatePathSegment(presetId, nameof(presetId));
                ValidatePathSegment(image.Id, nameof(image));
                string extension = NormalizeSupportedExtension(Path.GetExtension(image.RelativePath)); // 확인할 이미지 확장자
                string expectedPath = isDraft
                    ? BuildDraftImageRelativePath(presetId, image.Id, extension)
                    : BuildImageRelativePath(presetId, image.Id, extension); // 예상 이미지 상대 경로
                string normalizedPath = image.RelativePath.Replace('\\', '/'); // 정규화된 이미지 상대 경로
                return string.Equals(expectedPath, normalizedPath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                return false;
            }
        }

        // 이미지 데이터에서 원본 크기를 읽는 함수
        private static Vector2Int ReadImageSize(byte[] imageBytes)
        {
            Texture2D texture = CreateReadableTexture(imageBytes); // 크기 확인용 런타임 텍스처
            try
            {
                return new Vector2Int(texture.width, texture.height);
            }
            finally
            {
                DestroyTexture(texture);
            }
        }

        // 이미지 데이터로 읽기 가능한 텍스처를 생성하는 함수
        private static Texture2D CreateReadableTexture(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                throw new InvalidDataException("Image data is empty.");
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); // 디코딩 대상 텍스처
            if (!ImageConversion.LoadImage(texture, imageBytes, false))
            {
                DestroyTexture(texture);
                throw new InvalidDataException("Image data could not be decoded.");
            }

            return texture;
        }

        // 기본 프리셋 원본 PNG 데이터를 생성하는 함수
        private static byte[] CreateDefaultImageBytes()
        {
            Texture2D texture = new Texture2D( // 기본 프리셋 원본 텍스처
                DefaultImageSize,
                DefaultImageSize,
                TextureFormat.RGBA32,
                false);
            Color32[] pixels = new Color32[DefaultImageSize * DefaultImageSize]; // 기본 이미지 픽셀 목록
            float center = (DefaultImageSize - 1) * 0.5f; // 원형 중심 좌표
            float radiusSquared = center * center; // 원형 반지름 제곱

            for (int y = 0; y < DefaultImageSize; y++)
            {
                for (int x = 0; x < DefaultImageSize; x++)
                {
                    float offsetX = x - center; // 중심 기준 가로 거리
                    float offsetY = y - center; // 중심 기준 세로 거리
                    bool isInsideCircle = offsetX * offsetX + offsetY * offsetY <= radiusSquared; // 원 내부 여부
                    pixels[y * DefaultImageSize + x] = isInsideCircle
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            try
            {
                byte[] imageBytes = ImageConversion.EncodeToPNG(texture); // 인코딩된 기본 이미지 데이터
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    throw new InvalidDataException("Default preset image could not be encoded.");
                }

                return imageBytes;
            }
            finally
            {
                DestroyTexture(texture);
            }
        }

        // 프리셋 이미지의 상대 경로를 생성하는 함수
        private static string BuildImageRelativePath(
            string presetId,
            string imageId,
            string extension)
        {
            return string.Join(
                "/",
                PresetsDirectoryName,
                presetId,
                ImagesDirectoryName,
                imageId + extension);
        }

        // 편집 초안 이미지의 상대 경로를 생성하는 함수
        private static string BuildDraftImageRelativePath(
            string presetId,
            string imageId,
            string extension)
        {
            return string.Join(
                "/",
                DraftsDirectoryName,
                presetId,
                ImagesDirectoryName,
                imageId + extension);
        }

        // 프리셋 ID에 해당하는 절대 폴더 경로를 반환하는 함수
        private string GetPresetDirectory(string presetId)
        {
            ValidatePathSegment(presetId, nameof(presetId));
            return ResolveAbsolutePath(string.Join("/", PresetsDirectoryName, presetId));
        }

        // 프리셋 ID에 해당하는 편집 초안 폴더 경로를 반환하는 함수
        private string GetDraftDirectory(string presetId)
        {
            ValidatePathSegment(presetId, nameof(presetId));
            return ResolveAbsolutePath(string.Join("/", DraftsDirectoryName, presetId));
        }

        // 적용 완료 후 남은 초안 파일 정리를 시도하는 함수
        private void TryDiscardDraftAssets(string presetId)
        {
            try
            {
                DiscardDraftAssets(presetId);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"Failed to remove draft assets for preset '{presetId}': {exception.Message}");
            }
        }

        // 앱 데이터 루트 안의 안전한 절대 경로를 반환하는 함수
        private string ResolveAbsolutePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            {
                throw new InvalidDataException("Preset image path must be relative.");
            }

            string platformRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar); // 플랫폼 형식 상대 경로
            string absolutePath = Path.GetFullPath(Path.Combine(_dataRootPath, platformRelativePath)); // 확인할 절대 경로
            string rootPrefix = _dataRootPath.TrimEnd( // 앱 데이터 루트 접두 경로
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!absolutePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Preset image path escapes the data root.");
            }

            return absolutePath;
        }

        // 지원 이미지 확장자를 소문자 형식으로 반환하는 함수
        private static string NormalizeSupportedExtension(string extension)
        {
            string normalizedExtension = extension?.ToLowerInvariant(); // 정규화된 이미지 확장자
            if (normalizedExtension != ".png" &&
                normalizedExtension != ".jpg" &&
                normalizedExtension != ".jpeg")
            {
                throw new NotSupportedException("Only PNG, JPG, and JPEG images are supported.");
            }

            return normalizedExtension;
        }

        // 경로 조각으로 사용할 ID를 검증하는 함수
        private static void ValidatePathSegment(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                value.Contains(Path.DirectorySeparatorChar.ToString()) ||
                value.Contains(Path.AltDirectorySeparatorChar.ToString()) ||
                value == "." ||
                value == "..")
            {
                throw new ArgumentException("A safe preset ID is required.", parameterName);
            }
        }

        // 충돌하지 않는 내부 ID를 생성하는 함수
        private static string CreateInternalId()
        {
            return Guid.NewGuid().ToString("N");
        }

        // 초안 이미지 적용과 실패 복구를 관리하는 클래스
        internal sealed class DraftAssetCommit : IDisposable
        {
            private readonly PresetAssetStore _assetStore; // 프리셋 이미지 저장소
            private readonly string _presetId; // 적용 대상 프리셋 ID
            private readonly List<DraftAssetChange> _changes = new List<DraftAssetChange>(); // 확정 준비 이미지 목록
            private bool _isCompleted; // 적용 완료 여부

            // 적용 대상 프리셋 정보를 보관하는 생성자
            internal DraftAssetCommit(PresetAssetStore assetStore, string presetId)
            {
                _assetStore = assetStore;
                _presetId = presetId;
            }

            // 확정 준비한 이미지 변경을 기록하는 함수
            internal void Record(
                ImageAssetData image,
                string draftRelativePath,
                string ownedAbsolutePath)
            {
                _changes.Add(new DraftAssetChange(
                    image,
                    draftRelativePath,
                    ownedAbsolutePath));
            }

            // 적용을 확정하고 남은 초안 파일을 정리하는 함수
            internal void Complete()
            {
                _isCompleted = true;
                _assetStore.TryDiscardDraftAssets(_presetId);
            }

            // 미완료된 확정 준비 파일과 이미지 참조를 되돌리는 함수
            public void Dispose()
            {
                if (_isCompleted)
                {
                    return;
                }

                for (int index = _changes.Count - 1; index >= 0; index--)
                {
                    DraftAssetChange change = _changes[index]; // 되돌릴 이미지 변경
                    if (File.Exists(change.OwnedAbsolutePath))
                    {
                        File.Delete(change.OwnedAbsolutePath);
                    }

                    change.Image.RelativePath = change.DraftRelativePath;
                }
            }
        }

        // 확정 준비한 초안 이미지 변경을 보관하는 클래스
        private sealed class DraftAssetChange
        {
            internal readonly ImageAssetData Image; // 변경된 이미지 데이터
            internal readonly string DraftRelativePath; // 원래 초안 상대 경로
            internal readonly string OwnedAbsolutePath; // 복사된 소유 이미지 절대 경로

            // 초안 이미지 변경 정보를 보관하는 생성자
            internal DraftAssetChange(
                ImageAssetData image,
                string draftRelativePath,
                string ownedAbsolutePath)
            {
                Image = image;
                DraftRelativePath = draftRelativePath;
                OwnedAbsolutePath = ownedAbsolutePath;
            }
        }

        // 실행 환경에 맞게 런타임 텍스처를 정리하는 함수
        private static void DestroyTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(texture);
            }
            else
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
