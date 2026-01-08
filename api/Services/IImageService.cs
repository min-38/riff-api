namespace api.Services;

// 이미지 처리 및 업로드를 담당하는 서비스
public interface IImageService
{
    // S3에 단일 이미지를 업로드
    // 업로도된 이미지 url 리턴
    Task<string?> UploadImageAsync(Stream imageStream, string folder);
    // S3에 여러 이미지를 업로드
    // 업로드된 이미지 URL 배열 리턴
    Task<List<string>> UploadImagesAsync(List<Stream>? imageStreams, string folder);
    // S3에서 단일 이미지 삭제
    Task<bool> DeleteImageAsync(string imageUrl);
    // S3에서 다수 이미지 삭제
    // 삭제된 이미지 개수 리턴
    Task<int> DeleteImagesAsync(List<string>? imageUrls);
}
