using api.Models;

namespace api.Services;

public interface IPublicImageUrlService
{
    // 공개 URL 변환
    ImageData? ToPublicImageData(ImageData? images);
}
