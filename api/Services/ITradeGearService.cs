using System.Net;
using api.DTOs.Requests;
using api.DTOs.Responses;

namespace api.Services;

public interface ITradeGearService
{
    // 중고 장비 게시글 생성
    Task<GearResponse> CreateGearAsync(Guid sellerId, CreateGearRequest request, List<Stream>? imageStreams = null);
    // 중고 장비 게시글 목록 조회
    Task<GearListResponse> GetGearsAsync(GetGearsRequest request);
    // 중고 장비 게시글 조회
    Task<GearResponse> GetGearByIdAsync(long gearId, bool incrementViews = false, Guid? userId = null, IPAddress? ipAddress = null);
    // 중고 장비 게시글 수정
    Task<GearResponse> UpdateGearAsync(long gearId, Guid sellerId, UpdateGearRequest request, List<Stream>? imageStreams = null);
    // 중고 장비 게시글 삭제
    Task<bool> DeleteGearAsync(long gearId, Guid sellerId);
}
