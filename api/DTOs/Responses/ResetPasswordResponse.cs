namespace api.DTOs.Responses;

public class ResetPasswordResponse
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = null!;
}
