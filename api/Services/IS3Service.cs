namespace api.Services;

public interface IS3Service
{
    // 이미지 업로드
    Task<bool> UploadFileAsync(string fileName, Stream fileStream);
    // 이미지 다운로드
    Task<Stream?> DownloadFileAsync(string fileName);
    // 파일 존재 여부 확인
    Task<bool> FileExistsAsync(string fileKey);
    // 파일 메타데이터 조회
    Task<Dictionary<string, string>> GetFileMetadataAsync(string fileKey);
}
