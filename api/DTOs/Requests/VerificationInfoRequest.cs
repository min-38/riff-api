using System.ComponentModel.DataAnnotations;

namespace api.DTOs.Requests;

public class VerificationInfoRequest
{
    [Required(ErrorMessage = "Verification token is required")]
    public string VerificationToken { get; set; } = null!;
}
