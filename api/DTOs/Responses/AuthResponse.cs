namespace api.DTOs.Responses;

public class AuthResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = null!;
    public string? Token { get; set; }
    public string Nickname { get; set; } = null!;
    public Boolean Verified { get; set; } = false;
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
