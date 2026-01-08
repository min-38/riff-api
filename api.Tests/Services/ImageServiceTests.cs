using Microsoft.Extensions.Logging;
using Moq;
using api.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace api.Tests.Services;

public class ImageServiceTests
{
    private readonly Mock<IS3Service> _s3ServiceMock;
    private readonly Mock<ILogger<ImageService>> _loggerMock;
    private readonly ImageService _imageService;

    public ImageServiceTests()
    {
        _s3ServiceMock = new Mock<IS3Service>();
        _loggerMock = new Mock<ILogger<ImageService>>();
        _imageService = new ImageService(_s3ServiceMock.Object, _loggerMock.Object);
    }

    #region 단일 이미지 업로드 테스트

    [Fact]
    public async Task UploadImageAsync_ValidImage_Success()
    {
        // Arrange
        var folder = "test-folder";
        var imageStream = CreateTestImageStream(800, 600);


        _s3ServiceMock
            .Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>()))
            .ReturnsAsync(true);

        // Act
        var result = await _imageService.UploadImageAsync(imageStream, folder);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith(folder + "/", result);
        Assert.EndsWith(".jpg", result);

        _s3ServiceMock.Verify(
            x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>()),
            Times.Once
        );
    }

    [Fact]
    public async Task UploadImageAsync_ResizeFailed_ReturnsNull()
    {
        // Arrange
        var folder = "test-folder";
        var imageStream = new MemoryStream([0x01, 0x02, 0x03]);

        // Act
        var result = await _imageService.UploadImageAsync(imageStream, folder);

        // Assert
        Assert.Null(result);

        _s3ServiceMock.Verify(
            x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>()),
            Times.Never
        );
    }

    [Fact]
    public async Task UploadImageAsync_UploadFailed_ReturnsNull()
    {
        // Arrange
        var folder = "test-folder";
        var imageStream = CreateTestImageStream(800, 600);


        _s3ServiceMock
            .Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>()))
            .ReturnsAsync(false); // 업로드 실패

        // Act
        var result = await _imageService.UploadImageAsync(imageStream, folder);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UploadImageAsync_TooLargeFile_ReturnsNull()
    {
        // Arrange
        var folder = "test-folder";
        // 11MB 크기 (제한: 10MB)
        var largeStream = new MemoryStream(new byte[11 * 1024 * 1024]);

        // Act
        var result = await _imageService.UploadImageAsync(largeStream, folder);

        // Assert
        Assert.Null(result);

        _s3ServiceMock.Verify(
            x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>()),
            Times.Never
        );

    }

    #endregion

    #region 다중 이미지 업로드 테스트

    [Fact]
    public async Task UploadImagesAsync_ValidImages_Success()
    {
        // Arrange
        var folder = "test-folder";
        var imageStreams = new List<Stream>
        {
            CreateTestImageStream(800, 600),
            CreateTestImageStream(1024, 768),
            CreateTestImageStream(640, 480)
        };


        _s3ServiceMock
            .Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>()))
            .ReturnsAsync(true);

        // Act
        var results = await _imageService.UploadImagesAsync(imageStreams, folder);

        // Assert
        Assert.NotNull(results);
        Assert.Equal(3, results.Count);
        Assert.All(results, url =>
        {
            Assert.StartsWith(folder + "/", url);
            Assert.EndsWith(".jpg", url);
        });

        _s3ServiceMock.Verify(
            x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>()),
            Times.Exactly(3)
        );
    }

    [Fact]
    public async Task UploadImagesAsync_EmptyList_ReturnsEmptyList()
    {
        // Arrange
        var folder = "test-folder";
        var imageStreams = new List<Stream>();

        // Act
        var results = await _imageService.UploadImagesAsync(imageStreams, folder);

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);

        _s3ServiceMock.Verify(
            x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>()),
            Times.Never
        );

    }

    [Fact]
    public async Task UploadImagesAsync_NullList_ReturnsEmptyList()
    {
        // Arrange
        var folder = "test-folder";

        // Act
        var results = await _imageService.UploadImagesAsync(null, folder);

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task UploadImagesAsync_PartialFailure_ReturnsSuccessfulUploadsOnly()
    {
        // Arrange
        var folder = "test-folder";
        var imageStreams = new List<Stream>
        {
            CreateTestImageStream(800, 600),
            new MemoryStream([0xFF, 0xFF]), // 잘못된 이미지
            CreateTestImageStream(640, 480)
        };

        _s3ServiceMock
            .Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>()))
            .ReturnsAsync(true);

        // Act
        var results = await _imageService.UploadImagesAsync(imageStreams, folder);

        // Assert
        Assert.NotNull(results);
        Assert.Equal(2, results.Count); // 성공한 2개만 반환
    }

    #endregion

    #region 이미지 삭제 테스트

    [Fact]
    public async Task DeleteImageAsync_ValidUrl_ReturnsTrue()
    {
        // Arrange
        var imageUrl = "gears/test-image.jpg";

        // Act
        var result = await _imageService.DeleteImageAsync(imageUrl);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteImagesAsync_ValidUrls_ReturnsCount()
    {
        // Arrange
        var imageUrls = new List<string>
        {
            "gears/image-1.jpg",
            "gears/image-2.jpg",
            "gears/image-3.jpg"
        };

        // Act
        var result = await _imageService.DeleteImagesAsync(imageUrls);

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task DeleteImagesAsync_EmptyList_ReturnsZero()
    {
        // Arrange
        var imageUrls = new List<string>();

        // Act
        var result = await _imageService.DeleteImagesAsync(imageUrls);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task DeleteImagesAsync_NullList_ReturnsZero()
    {
        // Act
        var result = await _imageService.DeleteImagesAsync(null);

        // Assert
        Assert.Equal(0, result);
    }

    #endregion

    #region Helper Methods

    private static MemoryStream CreateTestImageStream(int width, int height)
    {
        var image = new Image<Rgba32>(width, height);
        var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        stream.Position = 0;
        image.Dispose();
        return stream;
    }

    #endregion
}
