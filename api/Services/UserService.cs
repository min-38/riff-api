using Microsoft.EntityFrameworkCore;
using api.Data;
using api.Models;

namespace api.Services;

public class UserService : IUserService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserService> _logger;

    public UserService(ApplicationDbContext context, ILogger<UserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // 이메일 사용 가능 여부 체크
    public async Task<bool> IsEmailAvailableAsync(string email)
    {
        // 활성 사용자 중복 체크 (대소문자 구분 안 함)
        var existingUser = await _context.Users
            .AnyAsync(u => u.Email.ToLower() == email.ToLower());

        if (existingUser)
        {
            _logger.LogInformation("Email {Email} already exists in Users table", email);
            return false;
        }
        return true;
    }

    // 닉네임 사용 가능 여부 체크
    public async Task<bool> IsNicknameAvailableAsync(string nickname)
    {
        // 활성 사용자 중복 체크
        var existingUser = await _context.Users
            .AnyAsync(u => u.Nickname == nickname);

        if (existingUser)
        {
            _logger.LogInformation("Nickname {Nickname} already exists in Users table", nickname);
            return false;
        }
        return true;
    }

    public async Task<User?> GetUserByPhoneAsync(string phone)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Phone == phone);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<User?> GetUserByNicknameAsync(string nickname)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Nickname == nickname);
    }
}
