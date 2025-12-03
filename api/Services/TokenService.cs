using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;


namespace api.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;

    public TokenService(IConfiguration configuration, ApplicationDbContext context)
    {
        _configuration = configuration;
        _context = context;
    }

    public string GenerateAccessToken(User user)
    {
        var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
            ?? throw new InvalidOperationException("JWT_SECRET_KEY is not configured");
        var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
            ?? throw new InvalidOperationException("JWT_ISSUER is not configured");
        var expirationMinutesStr = Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES")
            ?? throw new InvalidOperationException("JWT_EXPIRATION_MINUTES is not configured");

        if (!int.TryParse(expirationMinutesStr, out var expirationMinutes))
        {
            throw new InvalidOperationException("JWT_EXPIRATION_MINUTES must be a valid integer");
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("nickname", user.Nickname),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: null,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> GenerateRefreshTokenAsync(Guid userId)
    {
        // 랜덤 Refresh Token 생성 (128 bytes = 256 hex characters)
        var refreshToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(128));

        // DB에 저장
        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = refreshToken,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(7), // 7일 유효
            CreatedAt = DateTime.UtcNow,
            RevokedAt = null
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        return refreshToken;
    }

    // Refresh Token으로 새로운 Access Token 발급
    public async Task<(string AccessToken, string RefreshToken, User User)> RefreshAccessTokenAsync(string refreshToken)
    {
        // Refresh Token 조회
        var token = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (token == null)
            throw new InvalidOperationException("Invalid refresh token");

        // 토큰 유효성 검증
        if (!token.IsValid)
            throw new InvalidOperationException("Refresh token is expired or revoked");

        // 새로운 Access Token 생성 (TokenService 사용)
        var newAccessToken = GenerateAccessToken(token.User);

        // 새로운 Refresh Token 생성 (Refresh Token Rotation) (TokenService 사용)
        var newRefreshToken = await GenerateRefreshTokenAsync(token.User.Id);

        // 기존 Refresh Token 무효화
        token.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (newAccessToken, newRefreshToken, token.User);
    }
}
