using api.Models;

namespace api.Services;

public class PublicImageUrlService : IPublicImageUrlService
{
    private readonly string? _publicImageBaseUrl;

    public PublicImageUrlService()
    {
        _publicImageBaseUrl = Environment.GetEnvironmentVariable("S3_PUBLIC_BASE_URL");
    }

    public ImageData? ToPublicImageData(ImageData? images)
    {
        if (images == null)
            return null;

        if (string.IsNullOrWhiteSpace(_publicImageBaseUrl))
            return images;

        var baseUrl = _publicImageBaseUrl.TrimEnd('/');
        var mappedUrls = images.Urls
            .Select(url =>
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out _))
                    return url;

                return $"{baseUrl}/{url.TrimStart('/')}";
            })
            .ToList();

        return new ImageData
        {
            Count = images.Count,
            Urls = mappedUrls,
            MainIndex = images.MainIndex
        };
    }
}
