namespace api.DTOs.Responses;

public class ForgotPasswordResponse
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = null!;
}
