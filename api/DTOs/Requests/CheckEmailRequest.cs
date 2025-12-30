using System.ComponentModel.DataAnnotations;
using api.Validation;

namespace api.DTOs.Requests;

public class CheckEmailRequest
{
    [Required(ErrorMessage = "Email is required")]
    [ValidEmail]
    public string Email { get; set; } = null!;
}
