using System.Text.Json; // JSON serialization

namespace api.Services;

public class CaptchaService : ICaptchaService
{
    private readonly ILogger<CaptchaService> _logger;

    // HTTP 클라이언트를 생성하기 위한 팩토리
    // Turnstile 비밀 키 및 API URL 처리
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _turnstileSecretKey;
    private readonly string _turnstileApiUrl;

    public CaptchaService(
        ILogger<CaptchaService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;

        // .env에서 Secret Key를 읽어옴
        _turnstileSecretKey = Environment.GetEnvironmentVariable("TURNSTILE_SECRET_KEY")
            ?? configuration["Turnstile:SecretKey"]
            ?? throw new InvalidOperationException("Turnstile secret key is not configured");

        // .env에서 API URL을 읽어옴
        _turnstileApiUrl = Environment.GetEnvironmentVariable("TURNSTILE_API_URL")
            ?? "https://challenges.cloudflare.com/turnstile/v0/siteverify";
    }

    // Turnstile 토큰 검증
    public async Task<bool> VerifyTurnstileTokenAsync(string token, string? remoteIp = null)
    {
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Captcha token is null or empty");
            return false;
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient(); // HTTP 클라이언트 생성
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("secret", _turnstileSecretKey), // 비밀 키
                new KeyValuePair<string, string>("response", token), // 사용자 토큰
                new KeyValuePair<string, string>("remoteip", remoteIp ?? "") // 선택적 원격 IP
            });

            var response = await httpClient.PostAsync(_turnstileApiUrl, formData);

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Turnstile verification response: {Response}", responseBody);

            var result = JsonSerializer.Deserialize<TurnstileVerificationResponse>(responseBody);
            if (result?.Success == true)
            {
                _logger.LogInformation("Captcha verification successful");
                return true;
            }

            _logger.LogWarning("Captcha verification failed: {Errors}",
                result?.ErrorCodes != null ? string.Join(", ", result.ErrorCodes) : "Unknown error");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Turnstile token");
            return false;
        }
    }

    private class TurnstileVerificationResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("success")]
        public bool Success { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("challenge_ts")]
        public string? ChallengeTs { get; set; } // 인증 시간

        [System.Text.Json.Serialization.JsonPropertyName("hostname")]
        public string? Hostname { get; set; }
    }
}
