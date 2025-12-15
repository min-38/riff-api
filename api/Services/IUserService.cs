using api.Models;

namespace api.Services;


public interface IUserService
{
    // 이메일 중복 체크
    Task<User?> GetUserByEmailAsync(string email);

    // 닉네임 중복 체크
    Task<User?> GetUserByNicknameAsync(string nickname);

    // 핸드폰 번호 중복 체크
    Task<User?> GetUserByPhoneAsync(string phone);

    // 이메일 중복 체크 (활성 사용자)
    Task<bool> IsEmailAvailableAsync(string email);

    // 닉네임 중복 체크 (활성 사용자)
    Task<bool> IsNicknameAvailableAsync(string nickname);
}
