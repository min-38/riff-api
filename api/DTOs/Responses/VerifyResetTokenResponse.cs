namespace api.DTOs.Responses;

public class VerifyResetTokenResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
    public string? Email { get; set; } // 토큰이 유효한 경우 이메일 반환
}
