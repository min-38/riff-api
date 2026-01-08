using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using api.Data;
using api.DTOs.Requests;
using api.Models;
using api.Models.Enums;
using api.Services;
using System.Net;

namespace api.Tests.Integration;

/*
TradeGearService 통합 테스트
- 전체 플로우를 테스트하는 시나리오 기반 테스트
*/
public class TradeGearServiceIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<TradeGearService>> _loggerMock;
    private readonly Mock<IImageService> _imageServiceMock;
    private readonly Mock<IPublicImageUrlService> _publicImageUrlServiceMock;
    private ApplicationDbContext _context = null!;
    private TradeGearService _tradeGearService = null!;

    public TradeGearServiceIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<TradeGearService>>();
        _imageServiceMock = new Mock<IImageService>();
        _publicImageUrlServiceMock = new Mock<IPublicImageUrlService>();
        _publicImageUrlServiceMock
            .Setup(x => x.ToPublicImageData(It.IsAny<ImageData?>()))
            .Returns<ImageData?>(images => images);
    }

    private void InitializeContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _tradeGearService = new TradeGearService(
            _context,
            _loggerMock.Object,
            _imageServiceMock.Object,
            _publicImageUrlServiceMock.Object
        );
    }

    public void Dispose()
    {
        _context?.Database.EnsureDeleted();
        _context?.Dispose();
    }

    // Helper: 테스트용 User 생성
    private async Task<User> CreateTestUserAsync(string email = "test@test.com", string nickname = "testuser")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Nickname = nickname,
            Password = "hashed_password",
            Verified = true,
            Rating = 4.5,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        return user;
    }

    #region 전체 게시글 생성-조회-수정-삭제 플로우

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "TradeGear")]
    public async Task FullGearLifecycle_CreateReadUpdateDelete_Success()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();

        // 이미지 준비
        var imageStreams = new List<Stream>
        {
            new MemoryStream([0x01, 0x02, 0x03]),
            new MemoryStream([0x04, 0x05, 0x06])
        };

        var uploadedImageUrls = new List<string>
        {
            "gears/image-1.jpg",
            "gears/image-2.jpg"
        };

        _imageServiceMock
            .Setup(x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), "gears"))
            .ReturnsAsync(uploadedImageUrls);

        var createRequest = new CreateGearRequest
        {
            Title = "Fender Stratocaster",
            Description = "Excellent condition electric guitar from 2020",
            Price = 1500000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.LikeNew,
            TradeMethod = TradeMethod.Both,
            Region = Region.Seoul
        };

        // Act & Assert

        // 게시글 생성
        var createdGear = await _tradeGearService.CreateGearAsync(author.Id, createRequest, imageStreams);

        Assert.NotNull(createdGear);
        Assert.Equal(createRequest.Title, createdGear.Title);
        Assert.Equal(2, createdGear.Images!.Count);
        Assert.Equal(GearStatus.Selling, createdGear.Status);
        Assert.Equal(0, createdGear.ViewCount);

        // 게시글 조회 (조회수 증가)
        var viewerUserId = Guid.NewGuid();
        var ipAddress = IPAddress.Parse("192.168.1.1");
        var retrievedGear = await _tradeGearService.GetGearByIdAsync(
            createdGear.Id,
            incrementViews: true,
            userId: viewerUserId,
            ipAddress: ipAddress
        );

        Assert.NotNull(retrievedGear);
        Assert.Equal(createdGear.Id, retrievedGear.Id);
        Assert.Equal(1, retrievedGear.ViewCount); // 조회수 1 증가

        // 게시글 수정
        var newImageUrl = "gears/new-image.jpg";
        _imageServiceMock
            .Setup(x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), "gears"))
            .ReturnsAsync([newImageUrl]);

        var updateRequest = new UpdateGearRequest
        {
            Title = "Fender Stratocaster - Updated",
            Price = 1400000,
            KeepImageUrls = [uploadedImageUrls[0]] // 첫 번째 이미지만 유지
        };

        var newImageStream = new MemoryStream([0x07, 0x08, 0x09]);

        var updatedGear = await _tradeGearService.UpdateGearAsync(
            createdGear.Id,
            author.Id,
            updateRequest,
            [newImageStream]
        );

        Assert.NotNull(updatedGear);
        Assert.Equal("Fender Stratocaster - Updated", updatedGear.Title);
        Assert.Equal(1400000, updatedGear.Price);
        Assert.Equal(2, updatedGear.Images!.Count); // 유지한 1개 + 새 1개
        Assert.Equal(1, updatedGear.ViewCount); // 조회수는 유지

        // 목록 조회에서 확인
        var listRequest = new GetGearsRequest
        {
            Page = 1,
            PageSize = 10
        };

        var gearList = await _tradeGearService.GetGearsAsync(listRequest);

        Assert.Equal(1, gearList.TotalCount);
        Assert.Single(gearList.Gears);
        Assert.Equal("Fender Stratocaster - Updated", gearList.Gears[0].Title);

        // 게시글 삭제
        var deleteResult = await _tradeGearService.DeleteGearAsync(createdGear.Id, author.Id);

        Assert.True(deleteResult);

        // 삭제 후 조회 시 예외 발생
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _tradeGearService.GetGearByIdAsync(createdGear.Id)
        );

        // 목록에서도 제외됨
        var listAfterDelete = await _tradeGearService.GetGearsAsync(listRequest);

        Assert.Equal(0, listAfterDelete.TotalCount);
        Assert.Empty(listAfterDelete.Gears);
    }

    #endregion

    #region 복잡한 필터링 시나리오

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "TradeGear")]
    public async Task ComplexFilteringScenario_Success()
    {
        // Arrange
        InitializeContext();

        var seller1 = await CreateTestUserAsync("seller1@test.com", "seller1");
        var seller2 = await CreateTestUserAsync("seller2@test.com", "seller2");

        var imageUrls = new List<string> { "gears/test.jpg" };
        _imageServiceMock
            .Setup(x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), "gears"))
            .ReturnsAsync(imageUrls);

        // 다양한 게시글 생성
        var guitars = new[]
        {
            new { Title = "Fender Stratocaster", Price = 1500000, Region = Region.Seoul, Condition = GearCondition.LikeNew },
            new { Title = "Gibson Les Paul", Price = 2500000, Region = Region.Busan, Condition = GearCondition.Good },
            new { Title = "Ibanez RG", Price = 800000, Region = Region.Seoul, Condition = GearCondition.Fair }
        };

        foreach (var guitar in guitars)
        {
            var request = new CreateGearRequest
            {
                Title = guitar.Title,
                Description = $"{guitar.Title} for sale",
                Price = guitar.Price,
                Category = GearCategory.Instrument,
                SubCategory = GearSubCategory.Guitar,
                DetailCategory = GearDetailCategory.Electric,
                Condition = guitar.Condition,
                TradeMethod = TradeMethod.Both,
                Region = guitar.Region
            };

            await _tradeGearService.CreateGearAsync(
                seller1.Id,
                request,
                [new MemoryStream([0x01])]
            );
        }

        // Bass 추가
        var bassRequest = new CreateGearRequest
        {
            Title = "Fender Jazz Bass",
            Description = "Bass guitar for sale",
            Price = 1200000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Bass,
            DetailCategory = GearDetailCategory.ElectricBass,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        await _tradeGearService.CreateGearAsync(
            seller2.Id,
            bassRequest,
            [new MemoryStream([0x01])]
        );

        // Act & Assert

        // 카테고리 필터 (Guitar)
        var guitarFilter = new GetGearsRequest
        {
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            Page = 1,
            PageSize = 10
        };

        var guitarResults = await _tradeGearService.GetGearsAsync(guitarFilter);
        Assert.Equal(3, guitarResults.TotalCount);

        // 가격 범위 필터 (1,000,000 ~ 2,000,000)
        var priceFilter = new GetGearsRequest
        {
            MinPrice = 1000000,
            MaxPrice = 2000000,
            Page = 1,
            PageSize = 10
        };

        var priceResults = await _tradeGearService.GetGearsAsync(priceFilter);
        Assert.Equal(2, priceResults.TotalCount); // Stratocaster, Jazz Bass

        // 지역 필터 (서울)
        var regionFilter = new GetGearsRequest
        {
            Region = Region.Seoul,
            Page = 1,
            PageSize = 10
        };

        var regionResults = await _tradeGearService.GetGearsAsync(regionFilter);
        Assert.Equal(3, regionResults.TotalCount);

        // 복합 필터 (Guitar + Seoul + 가격)
        var complexFilter = new GetGearsRequest
        {
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            Region = Region.Seoul,
            MinPrice = 700000,
            MaxPrice = 1600000,
            Page = 1,
            PageSize = 10
        };

        var complexResults = await _tradeGearService.GetGearsAsync(complexFilter);
        Assert.Equal(2, complexResults.TotalCount); // Stratocaster, Ibanez

        // 검색어 필터 (Fender)
        var searchFilter = new GetGearsRequest
        {
            SearchKeyword = "fender",
            Page = 1,
            PageSize = 10
        };

        var searchResults = await _tradeGearService.GetGearsAsync(searchFilter);
        Assert.Equal(2, searchResults.TotalCount); // Stratocaster, Jazz Bass

        // 가격 정렬 (오름차순)
        var sortByPriceAsc = new GetGearsRequest
        {
            SortBy = "price",
            SortOrder = "asc",
            Page = 1,
            PageSize = 10
        };

        var sortedResults = await _tradeGearService.GetGearsAsync(sortByPriceAsc);
        Assert.Equal("Ibanez RG", sortedResults.Gears[0].Title);
        Assert.Equal("Gibson Les Paul", sortedResults.Gears[^1].Title);
    }

    #endregion

    #region 조회수 증가 시나리오

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "TradeGear")]
    public async Task ViewCountScenario_MultipleUsers_Success()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();

        _imageServiceMock
            .Setup(x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), "gears"))
            .ReturnsAsync(["gears/test.jpg"]);

        var request = new CreateGearRequest
        {
            Title = "Test Gear",
            Description = "Test description for view count test",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var gear = await _tradeGearService.CreateGearAsync(
            author.Id,
            request,
            [new MemoryStream([0x01])]
        );

        // Act & Assert

        // 첫 번째 사용자 조회
        var user1 = Guid.NewGuid();
        var ip1 = IPAddress.Parse("192.168.1.1");

        var view1 = await _tradeGearService.GetGearByIdAsync(gear.Id, true, user1, ip1);
        Assert.Equal(1, view1.ViewCount);

        // 같은 사용자 재조회 (조회수 유지)
        var view2 = await _tradeGearService.GetGearByIdAsync(gear.Id, true, user1, ip1);
        Assert.Equal(1, view2.ViewCount);

        // 다른 사용자 조회 (조회수 증가)
        var user2 = Guid.NewGuid();
        var ip2 = IPAddress.Parse("192.168.1.2");

        var view3 = await _tradeGearService.GetGearByIdAsync(gear.Id, true, user2, ip2);
        Assert.Equal(2, view3.ViewCount);

        // 비로그인 사용자 조회 (IP만으로 중복 체크)
        var ip3 = IPAddress.Parse("192.168.1.3");

        var view4 = await _tradeGearService.GetGearByIdAsync(gear.Id, true, null, ip3);
        Assert.Equal(3, view4.ViewCount);

        // 같은 IP 재조회 (조회수 유지)
        var view5 = await _tradeGearService.GetGearByIdAsync(gear.Id, true, null, ip3);
        Assert.Equal(3, view5.ViewCount);

        // DB에서 조회 기록 확인
        var viewRecords = await _context.TradeGearViews
            .Where(v => v.GearId == gear.Id)
            .ToListAsync();

        Assert.Equal(3, viewRecords.Count); // 3명의 유니크 뷰어
    }

    #endregion

    #region 권한 검증 시나리오

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "TradeGear")]
    public async Task AuthorizationScenario_UnauthorizedActions_Failure()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync("author@test.com", "author");
        var otherUser = await CreateTestUserAsync("other@test.com", "other");

        _imageServiceMock
            .Setup(x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), "gears"))
            .ReturnsAsync(["gears/test.jpg"]);

        var request = new CreateGearRequest
        {
            Title = "Author's Gear",
            Description = "This gear belongs to the author",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var gear = await _tradeGearService.CreateGearAsync(
            author.Id,
            request,
            [new MemoryStream([0x01])]
        );

        // Act & Assert

        // 다른 사용자의 수정 시도
        var updateRequest = new UpdateGearRequest
        {
            Title = "Hacked Title"
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _tradeGearService.UpdateGearAsync(gear.Id, otherUser.Id, updateRequest)
        );

        // 다른 사용자의 삭제 시도
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _tradeGearService.DeleteGearAsync(gear.Id, otherUser.Id)
        );

        // 작성자 본인의 수정
        var validUpdateRequest = new UpdateGearRequest
        {
            Title = "Updated by Author"
        };

        var updatedGear = await _tradeGearService.UpdateGearAsync(gear.Id, author.Id, validUpdateRequest);
        Assert.Equal("Updated by Author", updatedGear.Title);

        // 작성자 본인의 삭제
        var deleteResult = await _tradeGearService.DeleteGearAsync(gear.Id, author.Id);
        Assert.True(deleteResult);
    }

    #endregion

    #region 페이징 시나리오

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "TradeGear")]
    public async Task PaginationScenario_LargeDataset_Success()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();

        _imageServiceMock
            .Setup(x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), "gears"))
            .ReturnsAsync(["gears/test.jpg"]);

        // 20개의 게시글 생성
        for (int i = 1; i <= 20; i++)
        {
            var request = new CreateGearRequest
            {
                Title = $"Gear {i}",
                Description = $"Description for gear {i}",
                Price = i * 100000,
                Category = GearCategory.Instrument,
                SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
                Condition = GearCondition.Good,
                TradeMethod = TradeMethod.Direct,
                Region = Region.Seoul
            };

            await _tradeGearService.CreateGearAsync(
                author.Id,
                request,
                [new MemoryStream([0x01])]
            );
        }

        // Act & Assert

        // 첫 페이지 (1-10)
        var page1Request = new GetGearsRequest
        {
            Page = 1,
            PageSize = 10,
            SortBy = "created_at",
            SortOrder = "asc"
        };

        var page1 = await _tradeGearService.GetGearsAsync(page1Request);
        Assert.Equal(20, page1.TotalCount);
        Assert.Equal(10, page1.Gears.Count);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal("Gear 1", page1.Gears[0].Title);

        // 두 번째 페이지 (11-20)
        var page2Request = new GetGearsRequest
        {
            Page = 2,
            PageSize = 10,
            SortBy = "created_at",
            SortOrder = "asc"
        };

        var page2 = await _tradeGearService.GetGearsAsync(page2Request);
        Assert.Equal(20, page2.TotalCount);
        Assert.Equal(10, page2.Gears.Count);
        Assert.Equal("Gear 11", page2.Gears[0].Title);

        // 범위를 벗어난 페이지
        var page3Request = new GetGearsRequest
        {
            Page = 3,
            PageSize = 10
        };

        var page3 = await _tradeGearService.GetGearsAsync(page3Request);
        Assert.Equal(20, page3.TotalCount);
        Assert.Empty(page3.Gears);
    }

    #endregion
}
