using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Moq;
using api.Services;

namespace api.Tests.Services;

public class S3ServiceTests
{
    private readonly Mock<ILogger<S3Service>> _loggerMock;
    private readonly Mock<IAmazonS3> _s3ClientMock;
    private readonly string _bucketName = "test-bucket";

    public S3ServiceTests()
    {
        _loggerMock = new Mock<ILogger<S3Service>>();
        _s3ClientMock = new Mock<IAmazonS3>();
    }

    #region 파일 업로드/다운로드 테스트

    [Fact]
    public async Task UploadFileAsync_ValidFile_ReturnsTrue()
    {
        // Arrange
        var s3Service = new S3Service(_loggerMock.Object, _s3ClientMock.Object, _bucketName);
        var fileName = "test.jpg";
        var fileStream = new MemoryStream([0x01, 0x02, 0x03]);

        _s3ClientMock
            .Setup(client => client.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

        // Act
        var result = await s3Service.UploadFileAsync(fileName, fileStream);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DownloadFileAsync_ReturnsNull()
    {
        // Arrange
        var s3Service = new S3Service(_loggerMock.Object, _s3ClientMock.Object, _bucketName);
        var fileName = "test.jpg";

        _s3ClientMock
            .Setup(client => client.GetObjectAsync(_bucketName, fileName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("NotFound")
            {
                StatusCode = System.Net.HttpStatusCode.NotFound
            });

        // Act
        var result = await s3Service.DownloadFileAsync(fileName);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task FileExistsAsync_ReturnsFalse()
    {
        // Arrange
        var s3Service = new S3Service(_loggerMock.Object, _s3ClientMock.Object, _bucketName);
        var fileKey = "test.jpg";

        _s3ClientMock
            .Setup(client => client.GetObjectMetadataAsync(_bucketName, fileKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("NotFound")
            {
                StatusCode = System.Net.HttpStatusCode.NotFound
            });

        // Act
        var result = await s3Service.FileExistsAsync(fileKey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetFileMetadataAsync_ReturnsEmptyDictionary()
    {
        // Arrange
        var s3Service = new S3Service(_loggerMock.Object, _s3ClientMock.Object, _bucketName);
        var fileKey = "test.jpg";

        _s3ClientMock
            .Setup(client => client.GetObjectMetadataAsync(_bucketName, fileKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("metadata error"));

        // Act
        var result = await s3Service.GetFileMetadataAsync(fileKey);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion
}
