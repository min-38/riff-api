using api.Models.Enums;

namespace api.DTOs.Requests;

// 판매글 작성 요청 DTO
public class CreateGearRequest
{
    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public int Price { get; set; }

    public GearCategory Category { get; set; }

    public GearSubCategory SubCategory { get; set; }

    public GearDetailCategory DetailCategory { get; set; }

    public GearCondition? Condition { get; set; }

    public TradeMethod TradeMethod { get; set; }

    public Region Region { get; set; }

    // 메인 이미지 인덱스
    public int? MainImageIndex { get; set; }
}
