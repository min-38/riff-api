using System;
using System.Collections.Generic;

namespace api.Models;

public partial class User
{
    // PK
    public Guid Id { get; set; }

    // 인증 여부
    public bool Verified { get; set; }

    // 사용자 평점
    public double Rating { get; set; }

    // 사용자 아바타 URL(S3)
    public string? AvatarUrl { get; set; }

    // 생성 시간
    public DateTime CreatedAt { get; set; }

    // 수정 시간
    public DateTime UpdatedAt { get; set; }

    // 삭제 시간
    public DateTime? DeletedAt { get; set; }

    // 이메일
    public string Email { get; set; } = null!;

    // 비밀번호 (소셜 로그인 시 null)
    public string? Password { get; set; }

    // 닉네임
    public string Nickname { get; set; } = null!;

    // 전화번호 (선택 입력)
    public string? Phone { get; set; }

    // 이메일 인증 토큰
    public string? VerificationToken { get; set; }

    // 인증 토큰 만료 시간
    public DateTime? VerificationTokenExpiry { get; set; }

    // 인증 코드 (6자리 숫자)
    public string? VerificationCode { get; set; }

    // 마지막 인증 이메일 발송 시간 (Rate Limiting용)
    public DateTime? LastVerificationEmailSentAt { get; set; }

    // 인증 이메일 재발송 시도 횟수 (악의적 사용자 차단용)
    public int VerificationEmailAttempts { get; set; } = 0;

    // 일회용 회원가입 세션 토큰 (이메일 인증 후 회원가입 완료 시 사용)
    public string? RegistrationSessionToken { get; set; }

    // 회원가입 세션 토큰 만료 시간 (이메일 인증 후 30분)
    public DateTime? RegistrationSessionExpiry { get; set; }

    public virtual ICollection<Gear> Gears { get; set; } = new List<Gear>();
    public virtual ICollection<UserOAuth> UserOAuths { get; set; } = new List<UserOAuth>();
}
