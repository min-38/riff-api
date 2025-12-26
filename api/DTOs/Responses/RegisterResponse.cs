namespace api.DTOs.Responses;

public class RegisterResponse
{
    public string Message { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? VerificationToken { get; set; }
}
