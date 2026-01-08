namespace api.Constants;

// 이미지 처리 관련 상수
public static class ImageConstants
{
    // 허용된 이미지 MIME 타입 목록
    public static readonly HashSet<string> AllowedImageTypes = new()
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/bmp",
    };

    // 허용된 이미지 파일 확장자 목록
    public static readonly HashSet<string> AllowedImageExtensions = new()
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".webp",
        ".bmp",
        ".heic",
        ".heif"
    };

    // 최대 이미지 파일 크기 (MB)
    public const int MaxImageSizeMB = 10;

    // 최대 이미지 크기 (Bytes) 
    public const int MaxImageSizeBytes = MaxImageSizeMB * 1024 * 1024;

    // 최대 이미지 개수
    public const int MaxImageCount = 10;

    // 리사이징 최대 해상도
    public const int MaxDimension = 1080;

    // JPEG 압축 품질 (1~100)
    public const int JpegQuality = 80;
}
