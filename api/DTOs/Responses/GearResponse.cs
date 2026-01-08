using api.Models;
using api.Models.Enums;

namespace api.DTOs.Responses;

public class GearResponse
{
    public long Id { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public int Price { get; set; }
    public GearCategory Category { get; set; }
    public GearSubCategory SubCategory { get; set; }
    public GearDetailCategory DetailCategory { get; set; }
    public GearCondition? Condition { get; set; }
    public TradeMethod TradeMethod { get; set; }
    public Region Region { get; set; }
    public GearStatus Status { get; set; }
    public ImageData? Images { get; set; }
    public int ViewCount { get; set; }
    public int LikeCount { get; set; }
    public int ChatCount { get; set; }

    // 작성자 정보
    public Guid AuthorId { get; set; }
    public string AuthorNickname { get; set; } = null!;
    public double AuthorRating { get; set; }

    // 로그인 시 추가 정보 (비로그인 시 null)
    public bool? IsLiked { get; set; }
    public bool? IsAuthor { get; set; }

    // 시간 정보
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
