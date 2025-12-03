namespace api.DTOs.Responses;

public class UserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string Nickname { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public bool Verified { get; set; }
    public double Rating { get; set; }
    public DateTime CreatedAt { get; set; }
}
