namespace api.DTOs.Responses;

public class GearListResponse
{
    public List<GearResponse> Gears { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
