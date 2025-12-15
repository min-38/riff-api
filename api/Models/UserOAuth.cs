using System;

namespace api.Models;

// TODO: 나중에 구현
public partial class UserOAuth
{
    // PK (bigint)
    public long Id { get; set; }

    // FK - User 테이블
    public Guid UserId { get; set; }

    // OAuth Provider (email, google, kakao, naver)
    public string Provider { get; set; } = null!;

    // Provider에서 제공하는 고유 ID (소셜 로그인용, 이메일은 null)
    public string? ProviderId { get; set; }

    // 생성 시간
    public DateTime CreatedAt { get; set; }

    // 수정 시간
    public DateTime UpdatedAt { get; set; }

    // Navigation property
    public virtual User User { get; set; } = null!;
}
