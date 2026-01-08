using api.Models.Enums;

namespace api.DTOs.Requests;

public class UpdateGearRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int? Price { get; set; }
    public GearCategory? Category { get; set; }
    public GearSubCategory? SubCategory { get; set; }
    public GearDetailCategory? DetailCategory { get; set; }
    public GearCondition? Condition { get; set; }
    public TradeMethod? TradeMethod { get; set; }
    public Region? Region { get; set; }
    public GearStatus? Status { get; set; }

    // 유지할 기존 이미지 URL 리스트
    public List<string>? KeepImageUrls { get; set; }

    // 메인 이미지 인덱스
    public int? MainImageIndex { get; set; }
}
