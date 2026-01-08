namespace api.Models;

// 게시글 좋아요 엔티티
public class TradeGearLike
{
    public long Id { get; set; }

    public long GearId { get; set; }
    public TradeGear TradeGear { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
