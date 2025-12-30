using System;
using System.Collections.Generic;

namespace api.Models;

public partial class User
{
    // PK
    public Guid Id { get; set; }
    
    // 이메일
    public string Email { get; set; } = null!;

    // 비밀번호 (소셜 로그인 시 null)
    public string? Password { get; set; }

    // 닉네임
    public string Nickname { get; set; } = null!;

    // 전화번호 (선택 입력)
    public string? Phone { get; set; }

    // 인증 여부
    public bool Verified { get; set; }

    // 사용자 평점
    public double Rating { get; set; }

    // 사용자 아바타 URL(S3)
    public string? AvatarUrl { get; set; }

    // 이용약관 동의 여부 (필수)
    public bool TermsOfServiceAgreed { get; set; }

    // 개인정보처리방침 동의 여부 (필수)
    public bool PrivacyPolicyAgreed { get; set; }

    // 마케팅 수신 동의 여부 (선택)
    public bool MarketingAgreed { get; set; }

    // 이메일 인증 토큰
    public string? EmailVerificationToken { get; set; }

    // 이메일 인증 만료 시간
    public DateTime? EmailVerificationTokenExpiredAt { get; set; }

    // 비밀번호 재설정 토큰
    public string? PasswordResetToken { get; set; }

    // 비밀번호 재설정 토큰 만료 시간
    public DateTime? PasswordResetTokenExpiredAt { get; set; }

    // 생성 시간
    public DateTime CreatedAt { get; set; }

    // 수정 시간
    public DateTime UpdatedAt { get; set; }

    // 삭제 시간
    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<Gear> Gears { get; set; } = new List<Gear>();
    public virtual ICollection<UserOAuth> UserOAuths { get; set; } = new List<UserOAuth>();
}
