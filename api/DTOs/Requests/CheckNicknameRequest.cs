using System.ComponentModel.DataAnnotations;

namespace api.DTOs.Requests;

public class CheckNicknameRequest
{
    [Required(ErrorMessage = "Nickname is required")]
    [StringLength(15, MinimumLength = 2, ErrorMessage = "Nickname must be between 2 and 15 characters")]
    public string Nickname { get; set; } = null!;
}
