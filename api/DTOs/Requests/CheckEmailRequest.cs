using System.ComponentModel.DataAnnotations;

namespace api.DTOs.Requests;

public class CheckEmailRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = null!;
}
