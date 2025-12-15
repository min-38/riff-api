namespace api.Models;

public class BlockedUser
{
    public long Id { get; set; }
    public Guid? UserId { get; set; } // 특정 유저를 차단하는 경우
    public string? Email { get; set; } // 이메일 자체를 차단하는 경우
    public string? Reason { get; set; } // 차단 사유
    public DateTime BlockedAt { get; set; }
    public DateTime? ExpiresAt { get; set; } // null이면 영구 차단
    public string? BlockedBy { get; set; } // 차단한 관리자 또는 시스템

    // Navigation property
    public User? User { get; set; }
}
