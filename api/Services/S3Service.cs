using Amazon.S3;
using Amazon.S3.Model;
using System.Net;

namespace api.Services;

public class S3Service : IS3Service
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    private readonly ILogger<S3Service> _logger;

    public S3Service(ILogger<S3Service> logger, IAmazonS3 s3Client, string bucketName)
    {
        _logger = logger;
        _s3Client = s3Client;
        _bucketName = bucketName;
    }

    // 이미지 업로드
    public async Task<bool> UploadFileAsync(string fileName, Stream fileStream)
    {
        try
        {
            if (fileStream.CanSeek)
                fileStream.Position = 0;

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = fileName,
                InputStream = fileStream,
                ContentType = GetContentType(fileName),
                AutoCloseStream = false
            };

            request.Headers.CacheControl = "public, max-age=31536000";
            if (fileStream.CanSeek)
            {
                request.Headers.ContentLength = fileStream.Length;
            }
            TrySetRequestBool(request, "DisablePayloadSigning", true);
            TrySetRequestBool(request, "UseChunkEncoding", false);

            var response = await _s3Client.PutObjectAsync(request);
            return response.HttpStatusCode == HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file to S3: {FileName}", fileName);
            return false;
        }
    }

    // 이미지 다운로드
    public async Task<Stream?> DownloadFileAsync(string fileName)
    {
        try
        {
            var response = await _s3Client.GetObjectAsync(_bucketName, fileName);
            await using var responseStream = response.ResponseStream;

            var memoryStream = new MemoryStream();
            await responseStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            return memoryStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file from S3: {FileName}", fileName);
            return null;
        }
    }

    // 파일 존재 여부 확인
    public async Task<bool> FileExistsAsync(string fileKey)
    {
        try
        {
            await _s3Client.GetObjectMetadataAsync(_bucketName, fileKey);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check file existence in S3: {FileKey}", fileKey);
            return false;
        }
    }

    // 파일 메타데이터 조회
    public async Task<Dictionary<string, string>> GetFileMetadataAsync(string fileKey)
    {
        try
        {
            var response = await _s3Client.GetObjectMetadataAsync(_bucketName, fileKey);

            var metadata = new Dictionary<string, string>
            {
                { "ContentLength", response.ContentLength.ToString() },
                { "ContentType", response.Headers.ContentType }
            };

            foreach (var key in response.Metadata.Keys)
            {
                metadata[key] = response.Metadata[key];
            }

            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file metadata from S3: {FileKey}", fileKey);
            return new Dictionary<string, string>();
        }
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    private static void TrySetRequestBool(object request, string propertyName, bool value)
    {
        var prop = request.GetType().GetProperty(propertyName);
        if (prop != null && prop.PropertyType == typeof(bool) && prop.CanWrite)
            prop.SetValue(request, value);
    }
}
