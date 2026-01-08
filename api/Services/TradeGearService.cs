using System.Net;
using api.Data;
using api.Models;
using api.Models.Enums;
using api.DTOs.Requests;
using api.DTOs.Responses;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public class TradeGearService : ITradeGearService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TradeGearService> _logger;
    private readonly IImageService _imageService;
    private readonly IPublicImageUrlService _publicImageUrlService;

    public TradeGearService(
        ApplicationDbContext context,
        ILogger<TradeGearService> logger,
        IImageService imageService,
        IPublicImageUrlService publicImageUrlService)
    {
        _context = context;
        _logger = logger;
        _imageService = imageService;
        _publicImageUrlService = publicImageUrlService;
    }

    public async Task<GearResponse> CreateGearAsync(Guid sellerId, CreateGearRequest request, List<Stream>? imageStreams = null)
    {
        // 이미지 필수 검증 (최소 1개 이상)
        if (imageStreams == null || imageStreams.Count == 0)
            throw new InvalidOperationException("At least one image is required");

        // 작성자가 실졔 있는 계정이고 실제 본인인지 확인
        var authorExists = await _context.Users.AnyAsync(u => u.Id == sellerId && u.DeletedAt == null);
        if (!authorExists)
            throw new InvalidOperationException("Author not found");

        // 이미지 업로드 처리
        var imageUrls = await _imageService.UploadImagesAsync(imageStreams, "gears");
        if (imageUrls.Count != imageStreams.Count)
            throw new InvalidOperationException("Failed to upload all images");

        // 이미지 데이터 생성
        ImageData? imageData = null;
        if (imageUrls.Count > 0)
        {
            var mainIndex = request.MainImageIndex ?? 0;
            if (mainIndex < 0 || mainIndex >= imageUrls.Count)
            {
                mainIndex = 0;
            }

            imageData = new ImageData
            {
                Count = imageUrls.Count,
                Urls = imageUrls,
                MainIndex = mainIndex
            };
        }

        // Gear 엔티티 생성
        var tradegear = new TradeGear
        {
            Title = request.Title,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            SubCategory = request.SubCategory,
            DetailCategory = request.DetailCategory,
            Condition = request.Condition,
            TradeMethod = request.TradeMethod,
            Region = request.Region,
            Status = GearStatus.Selling,
            Images = imageData,
            ViewCount = 0,
            LikeCount = 0,
            AuthorId = sellerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TradeGears.Add(tradegear);
        await _context.SaveChangesAsync();

        _logger.LogInformation("TradeGear created: {TradeGearId} by author {AuthorId}", tradegear.Id, sellerId);

        // Response 생성을 위해 관련 데이터 조회
        return await GetGearByIdAsync(tradegear.Id);
    }

    public async Task<GearListResponse> GetGearsAsync(GetGearsRequest request)
    {
        var query = _context.TradeGears
            .Include(g => g.Author)
            .Where(g => g.DeletedAt == null)
            .AsQueryable();

        // 필터링
        if (request.Category.HasValue)
            query = query.Where(g => g.Category == request.Category.Value);

        if (request.SubCategory.HasValue)
            query = query.Where(g => g.SubCategory == request.SubCategory.Value);

        if (request.DetailCategory.HasValue)
            query = query.Where(g => g.DetailCategory == request.DetailCategory.Value);

        if (request.Status.HasValue)
            query = query.Where(g => g.Status == request.Status.Value);

        if (request.Condition.HasValue)
            query = query.Where(g => g.Condition == request.Condition.Value);

        if (request.TradeMethod.HasValue)
            query = query.Where(g => g.TradeMethod == request.TradeMethod.Value);

        if (request.Region.HasValue)
            query = query.Where(g => g.Region == request.Region.Value);

        if (request.MinPrice.HasValue)
            query = query.Where(g => g.Price >= request.MinPrice.Value);

        if (request.MaxPrice.HasValue)
            query = query.Where(g => g.Price <= request.MaxPrice.Value);

        if (!string.IsNullOrEmpty(request.SearchKeyword))
        {
            var keyword = request.SearchKeyword.ToLower();
            query = query.Where(g => g.Title.ToLower().Contains(keyword) ||
                                     g.Description.ToLower().Contains(keyword));
        }

        // 정렬
        query = request.SortBy.ToLower() switch
        {
            "price" => request.SortOrder.ToLower() == "asc"
                ? query.OrderBy(g => g.Price)
                : query.OrderByDescending(g => g.Price),
            "view_count" => request.SortOrder.ToLower() == "asc"
                ? query.OrderBy(g => g.ViewCount)
                : query.OrderByDescending(g => g.ViewCount),
            _ => request.SortOrder.ToLower() == "asc"
                ? query.OrderBy(g => g.CreatedAt)
                : query.OrderByDescending(g => g.CreatedAt)
        };

        // 전체 개수
        var totalCount = await query.CountAsync();

        // 페이징
        var gears = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(g => new GearResponse
            {
                Id = g.Id,
                Title = g.Title,
                Description = g.Description,
                Price = g.Price,
                Category = g.Category,
                SubCategory = g.SubCategory,
                DetailCategory = g.DetailCategory,
                Condition = g.Condition,
                TradeMethod = g.TradeMethod,
                Region = g.Region,
                Status = g.Status,
                Images = g.Images,
                ViewCount = g.ViewCount,
                LikeCount = g.LikeCount,
                ChatCount = g.ChatCount,
                AuthorId = g.AuthorId,
                AuthorNickname = g.Author.Nickname,
                AuthorRating = g.Author.Rating,
                CreatedAt = g.CreatedAt,
                UpdatedAt = g.UpdatedAt
            })
            .ToListAsync();

        foreach (var gear in gears)
            gear.Images = _publicImageUrlService.ToPublicImageData(gear.Images);

        return new GearListResponse
        {
            Gears = gears,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
        };
    }

    public async Task<GearResponse> GetGearByIdAsync(long gearId, bool incrementViews = false, Guid? userId = null, IPAddress? ipAddress = null)
    {
        var gear = await _context.TradeGears
            .Include(g => g.Author)
            .FirstOrDefaultAsync(g => g.Id == gearId && g.DeletedAt == null);

        if (gear == null)
            throw new InvalidOperationException("Gear not found");

        // 조회수 증가 로직
        if (incrementViews)
        {
            bool isDuplicateView = false;

            // 로그인 유저는 user_id 또는 IP 중 하나라도 매칭되면 중복
            if (userId.HasValue)
            {
                isDuplicateView = await _context.TradeGearViews.AnyAsync(gv =>
                    gv.GearId == gearId &&
                    (gv.UserId == userId.Value ||
                     (ipAddress != null && gv.IpAddress != null && gv.IpAddress.Equals(ipAddress))));
            }
            // 비로그인 유저는 IP로 중복 체크
            else if (ipAddress != null)
            {
                isDuplicateView = await _context.TradeGearViews.AnyAsync(gv =>
                    gv.GearId == gearId &&
                    gv.IpAddress != null &&
                    gv.IpAddress.Equals(ipAddress));
            }

            // 중복이 아니면 조회 기록 추가 및 ViewCount 증가
            if (!isDuplicateView)
            {
                var gearView = new TradeGearView
                {
                    GearId = gearId,
                    UserId = userId,
                    IpAddress = ipAddress,
                    ViewedAt = DateTime.UtcNow
                };

                _context.TradeGearViews.Add(gearView);
                gear.ViewCount++;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Gear view recorded: GearId={GearId}, UserId={UserId}, IP={IpAddress}",
                    gearId, userId, ipAddress);
            }
        }

        // 로그인한 경우 추가 정보 조회
        bool? isLiked = null;
        bool? isAuthor = null;

        if (userId.HasValue)
        {
            // 좋아요 여부 확인
            isLiked = await _context.TradeGearLikes.AnyAsync(gl =>
                gl.GearId == gearId && gl.UserId == userId.Value);

            // 본인 게시글 여부 확인
            isAuthor = gear.AuthorId == userId.Value;
        }

        return new GearResponse
        {
            Id = gear.Id,
            Title = gear.Title,
            Description = gear.Description,
            Price = gear.Price,
            Category = gear.Category,
            SubCategory = gear.SubCategory,
            DetailCategory = gear.DetailCategory,
            Condition = gear.Condition,
            TradeMethod = gear.TradeMethod,
            Region = gear.Region,
            Status = gear.Status,
            Images = _publicImageUrlService.ToPublicImageData(gear.Images),
            ViewCount = gear.ViewCount,
            LikeCount = gear.LikeCount,
            ChatCount = gear.ChatCount,
            AuthorId = gear.AuthorId,
            AuthorNickname = gear.Author.Nickname,
            AuthorRating = gear.Author.Rating,
            IsLiked = isLiked,
            IsAuthor = isAuthor,
            CreatedAt = gear.CreatedAt,
            UpdatedAt = gear.UpdatedAt
        };
    }

    public async Task<GearResponse> UpdateGearAsync(long gearId, Guid sellerId, UpdateGearRequest request, List<Stream>? imageStreams = null)
    {
        var gear = await _context.TradeGears
            .FirstOrDefaultAsync(g => g.Id == gearId && g.DeletedAt == null);
        if (gear == null)
            throw new InvalidOperationException("Gear not found");

        // 작성자 본인 확인
        if (gear.AuthorId != sellerId)
            throw new UnauthorizedAccessException("You are not authorized to update this gear");

        // 이미지 업데이트 처리
        if (imageStreams != null || request.KeepImageUrls != null || request.MainImageIndex.HasValue)
        {
            var existingUrls = gear.Images?.Urls ?? new List<string>(); // 기존에 있는 이미지
            var keepUrls = request.KeepImageUrls ?? new List<string>(existingUrls); // 유지할 이미지
            var newImageUrls = new List<string>(); // 새로운 이미지
            var expectedNewCount = imageStreams?.Count ?? 0;
            var expectedFinalCount = keepUrls.Count + expectedNewCount;

            if (expectedFinalCount == 0)
                throw new InvalidOperationException("At least one image is required");

            var mainIndex = 0;
            if (request.MainImageIndex.HasValue)
            {
                var requestedIndex = request.MainImageIndex.Value;
                if (requestedIndex < 0 || requestedIndex >= expectedFinalCount)
                    throw new InvalidOperationException("Invalid main image index");

                mainIndex = requestedIndex;
            }
            else if (gear.Images != null && gear.Images.Urls.Count > 0)
            {
                var existingMainIndex = gear.Images.MainIndex;
                if (existingMainIndex >= 0 && existingMainIndex < gear.Images.Urls.Count)
                {
                    var existingMainUrl = gear.Images.Urls[existingMainIndex];
                    var preservedIndex = keepUrls.IndexOf(existingMainUrl);
                    if (preservedIndex >= 0)
                        mainIndex = preservedIndex;
                }
            }

            // 새 이미지 업로드
            if (imageStreams != null && imageStreams.Count > 0)
            {
                newImageUrls = await _imageService.UploadImagesAsync(imageStreams, "gears");
                if (newImageUrls.Count != imageStreams.Count)
                    throw new InvalidOperationException("Failed to upload all images");
            }

            // 삭제할 이미지 찾기 (기존 이미지 중 유지 목록에 없는 것들)
            if (gear.Images != null && gear.Images.Urls.Count > 0)
            {
                var imagesToDelete = gear.Images.Urls
                    .Where(url => !keepUrls.Contains(url))
                    .ToList();

                if (imagesToDelete.Count > 0)
                    await _imageService.DeleteImagesAsync(imagesToDelete);
            }

            // 최종 이미지 = 유지할 이미지 + 새 이미지
            var finalImageUrls = new List<string>();
            finalImageUrls.AddRange(keepUrls);
            finalImageUrls.AddRange(newImageUrls);

            // 이미지 데이터 업데이트
            gear.Images = new ImageData
            {
                Count = finalImageUrls.Count,
                Urls = finalImageUrls,
                MainIndex = mainIndex
            };
        }

        // 나머지 필드 업데이트
        if (request.Title != null) gear.Title = request.Title;
        if (request.Description != null) gear.Description = request.Description;
        if (request.Price.HasValue) gear.Price = request.Price.Value;
        if (request.Category.HasValue) gear.Category = request.Category.Value;
        if (request.SubCategory.HasValue) gear.SubCategory = request.SubCategory.Value;
        if (request.DetailCategory.HasValue) gear.DetailCategory = request.DetailCategory.Value;
        if (request.Condition.HasValue) gear.Condition = request.Condition;
        if (request.TradeMethod.HasValue) gear.TradeMethod = request.TradeMethod.Value;
        if (request.Region.HasValue) gear.Region = request.Region.Value;
        if (request.Status.HasValue) gear.Status = request.Status.Value;

        gear.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Gear updated: {GearId} by author {AuthorId}", gearId, sellerId);

        return await GetGearByIdAsync(gearId);
    }

    public async Task<bool> DeleteGearAsync(long gearId, Guid sellerId)
    {
        var gear = await _context.TradeGears
            .FirstOrDefaultAsync(g => g.Id == gearId && g.DeletedAt == null);
        if (gear == null)
            throw new InvalidOperationException("Gear not found");

        // 작성자 본인 확인
        if (gear.AuthorId != sellerId)
            throw new UnauthorizedAccessException("You are not authorized to delete this gear");

        // Soft Delete
        gear.DeletedAt = DateTime.UtcNow;
        gear.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Gear deleted (soft): {GearId} by author {AuthorId}", gearId, sellerId);

        return true;
    }

}
