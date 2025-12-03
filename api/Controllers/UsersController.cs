using Microsoft.AspNetCore.Mvc;
using api.DTOs.Responses;
using api.Data;
using api.Services;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(ApplicationDbContext context, IUserService userService, ILogger<UsersController> logger)
    {
        _context = context;
        _userService = userService;
        _logger = logger;
    }

    // 이메일 중복 체크
    [HttpGet("check-email")]
    public async Task<IActionResult> CheckEmail([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Email is required" });

        var isAvailable = await _userService.IsEmailAvailableAsync(email);

        return Ok(new
        {
            email,
            available = isAvailable,
            message = isAvailable ? "Email is available" : "Email already exists"
        });
    }

    // 닉네임 중복 체크
    [HttpGet("check-nickname")]
    public async Task<IActionResult> CheckNickname([FromQuery] string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return BadRequest(new { message = "Nickname is required" });

        var isAvailable = await _userService.IsNicknameAvailableAsync(nickname);

        return Ok(new
        {
            nickname,
            available = isAvailable,
            message = isAvailable ? "Nickname is available" : "Nickname already exists"
        });
    }
}
