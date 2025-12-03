using System.ComponentModel.DataAnnotations;

namespace api.DTOs.Requests;

public class RefreshTokenRequest
{
    [Required(ErrorMessage = "RefreshToken is required")]
    public string RefreshToken { get; set; } = null!;
}
