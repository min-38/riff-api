using System;

namespace api.Models;

public class RefreshToken
{
    // PK
    public Guid Id { get; set; }

    // Refresh Token 문자열
    public string Token { get; set; } = null!;

    // 사용자 FK
    public Guid UserId { get; set; }

    // 만료 시간
    public DateTime ExpiresAt { get; set; }

    // 생성 시간
    public DateTime CreatedAt { get; set; }

    // 무효화 시간 (로그아웃 또는 강제 무효화)
    public DateTime? RevokedAt { get; set; }

    // Navigation property
    public virtual User User { get; set; } = null!;

    // 유효한 토큰인지 확인
    public bool IsValid => RevokedAt == null && ExpiresAt > DateTime.UtcNow;
}
