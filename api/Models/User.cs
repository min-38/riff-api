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

    // 비밀번호
    public string Password { get; set; } = null!;

    // 닉네임
    public string Nickname { get; set; } = null!;

    // 전화번호
    public string Phone { get; set; } = null!;

    // 이메일 인증 토큰
    public string? VerificationToken { get; set; }

    // 인증 토큰 만료 시간
    public DateTime? VerificationTokenExpiry { get; set; }

    public virtual ICollection<Gear> Gears { get; set; } = new List<Gear>();
}
