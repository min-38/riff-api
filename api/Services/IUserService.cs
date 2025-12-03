using api.Models;

namespace api.Services;


public interface IUserService
{
    // 이메일 중복 체크 (활성 사용자 + 대기 사용자)
    Task<User?> GetUserByEmailAsync(string email);

    // 닉네임 중복 체크 (활성 사용자 + 대기 사용자)
    Task<User?> GetUserByNicknameAsync(string nickname);

    // 이메일 중복 체크 (활성 사용자)
    Task<bool> IsEmailAvailableAsync(string email);

    // 닉네임 중복 체크 (활성 사용자)
    Task<bool> IsNicknameAvailableAsync(string nickname);
}
