namespace api.DTOs.Responses;

public class VerificationInfoResponse
{
    public string Email { get; set; } = null!;
    public DateTime? SentAt { get; set; }
}
