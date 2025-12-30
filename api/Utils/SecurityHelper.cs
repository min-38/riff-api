using System.Security.Cryptography;
using System.Text;

namespace api.Utils;

public static class SecurityHelper
{
    // 6자리 인증 코드 생성 (100000 ~ 999999)
    public static string GenerateVerificationCode()
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }

    // 보안 세션 토큰 생성 (32바이트)
    public static string GenerateSessionToken()
    {
        // Base64 인코딩
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    // 검증 토큰 생성 (32바이트)
    public static string GenerateVerificationToken()
    {
        var base64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        // URL에 영향이 가는 특수문자 변경
        return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    // 토큰 해싱 (SHA256)
    public static string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashBytes);
    }
}
