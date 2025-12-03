using api.Models;

namespace api.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    Task<string> GenerateRefreshTokenAsync(Guid userId);

    Task<(string AccessToken, string RefreshToken, User User)> RefreshAccessTokenAsync(string refreshToken);
}
