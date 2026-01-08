using api.Models.Enums;

namespace api.DTOs.Requests;

public class GetGearsRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    // 필터링 옵션
    public GearCategory? Category { get; set; }
    public GearSubCategory? SubCategory { get; set; }
    public GearDetailCategory? DetailCategory { get; set; }
    public GearStatus? Status { get; set; }
    public GearCondition? Condition { get; set; }
    public TradeMethod? TradeMethod { get; set; }
    public Region? Region { get; set; }

    // 가격 범위
    public int? MinPrice { get; set; }
    public int? MaxPrice { get; set; }

    // 검색어
    public string? SearchKeyword { get; set; }

    // 정렬
    public string SortBy { get; set; } = "created_at"; // created_at, price, view_count
    public string SortOrder { get; set; } = "desc"; // asc, desc
}
