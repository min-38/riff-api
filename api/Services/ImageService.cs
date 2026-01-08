using api.Constants;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace api.Services;

public class ImageService : IImageService
{
    private readonly IS3Service _s3Service;
    private readonly ILogger<ImageService> _logger;

    public ImageService(IS3Service s3Service, ILogger<ImageService> logger)
    {
        _s3Service = s3Service;
        _logger = logger;
    }

    public async Task<string?> UploadImageAsync(Stream imageStream, string folder)
    {
        try
        {
            // 파일 크기 체크
            if (imageStream.CanSeek && imageStream.Length > ImageConstants.MaxImageSizeBytes)
            {
                _logger.LogWarning("Image too large ({Size}MB), max allowed is {MaxSize}MB",
                    imageStream.Length / 1024 / 1024, ImageConstants.MaxImageSizeMB);
                return null;
            }

            // 실제 이미지 파일인지 검증
            // 확장자만 바꾼 파일인지 검증
            var resizedStream = await ResizeImageAsync(imageStream, ImageConstants.MaxDimension, ImageConstants.MaxDimension);

            if (resizedStream == null)
            {
                _logger.LogWarning("Invalid image file or unsupported format (HEIC/HEIF are not supported)");
                return null;
            }

            // S3 업로드
            var fileName = $"{folder}/{Guid.NewGuid()}.jpg";
            var uploadSuccess = await _s3Service.UploadFileAsync(fileName, resizedStream);

            // Dispose는 스트림이 잡고 있는 관리되지 않는 리소스를 해제하고 메모리 사용을 줄이는 작업
            // S3 업로드가 끝나면 더 이상 사용할 일이 없으므로 즉시 Dispose해 메모리 회수
            await resizedStream.DisposeAsync();

            if (uploadSuccess)
            {
                _logger.LogInformation("Image uploaded successfully: {FileName}", fileName);
                return fileName;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process and upload image");
            return null;
        }
    }

    public async Task<List<string>> UploadImagesAsync(List<Stream>? imageStreams, string folder)
    {
        var imageUrls = new List<string>();

        if (imageStreams == null || imageStreams.Count == 0)
            return imageUrls;

        foreach (Stream imageStream in imageStreams)
        {
            var imageUrl = await UploadImageAsync(imageStream, folder);
            if (imageUrl != null)
                imageUrls.Add(imageUrl);
        }

        return imageUrls;
    }

    public Task<bool> DeleteImageAsync(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return Task.FromResult(false);

        return Task.FromResult(true);
    }

    public async Task<int> DeleteImagesAsync(List<string>? imageUrls)
    {
        if (imageUrls == null || imageUrls.Count == 0)
            return 0;

        var deletedCount = 0;
        foreach (var imageUrl in imageUrls)
        {
            if (await DeleteImageAsync(imageUrl))
                deletedCount++;
        }

        return deletedCount;
    }

    // 이미지 리사이징 및 압축
    private async Task<Stream?> ResizeImageAsync(Stream imageStream, int maxWidth, int maxHeight)
    {
        try
        {
            // 원본 파일 크기 측정
            long originalSize = 0;
            if (imageStream.CanSeek)
            {
                originalSize = imageStream.Length;
                imageStream.Position = 0;
            }

            // 이미지 로드
            using var image = await Image.LoadAsync(imageStream);
            var originalWidth = image.Width;
            var originalHeight = image.Height;

            // 비율 유지하면서 리사이징
            var ratioX = (double)maxWidth / image.Width;
            var ratioY = (double)maxHeight / image.Height;
            var ratio = Math.Min(ratioX, ratioY);

            // 리사이징이 필요한 경우에만 처리
            bool needsResize = ratio < 1.0;
            if (needsResize)
            {
                var newWidth = (int)(image.Width * ratio);
                var newHeight = (int)(image.Height * ratio);

                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(newWidth, newHeight),
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Lanczos3 // 고품질 리샘플링
                }));
            }

            // JPEG로 변환하여 메모리 스트림에 저장
            var outputStream = new MemoryStream();
            await image.SaveAsJpegAsync(outputStream, new JpegEncoder
            {
                Quality = ImageConstants.JpegQuality
            });

            var compressedSize = outputStream.Length;
            outputStream.Position = 0;

            // 압축 결과 로깅
            var compressionRatio = originalSize > 0
                ? (1 - (double)compressedSize / originalSize) * 100
                : 0;

            _logger.LogInformation(
                "Image processed: {OriginalWidth}x{OriginalHeight} -> {FinalWidth}x{FinalHeight}, " +
                "Size: {OriginalSize}KB -> {CompressedSize}KB ({CompressionRatio:F1}% reduced)",
                originalWidth, originalHeight, image.Width, image.Height,
                originalSize / 1024, compressedSize / 1024, compressionRatio);

            return outputStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resize and compress image");
            return null;
        }
    }
}
