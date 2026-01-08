using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using api.Data;
using api.DTOs.Requests;
using api.Models;
using api.Models.Enums;
using api.Services;
using DotNetEnv;
using System.Net;
using Amazon.Auth.AccessControlPolicy;

namespace api.Tests.Services;

public class TradeGearServiceTests : IDisposable
{
    private readonly Mock<ILogger<TradeGearService>> _loggerMock;
    private readonly Mock<ITradeGearService> _tradeGearServiceMock;
    private readonly Mock<IImageService> _imageServiceMock;
    private readonly Mock<IPublicImageUrlService> _publicImageUrlServiceMock;
    private ApplicationDbContext? _context;
    private TradeGearService _tradeGearService = null!;

    public TradeGearServiceTests()
    {
        Env.Load();

        _loggerMock = new Mock<ILogger<TradeGearService>>();
        _tradeGearServiceMock = new Mock<ITradeGearService>();
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

    // *************** Helper ***************
    // 테스트용 User 생성
    private async Task<User> CreateTestUserAsync(string email = "test@test.com", string nickname = "testuser", double rating = 4.5)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Nickname = nickname,
            Password = "hashed_password",
            Verified = true,
            Rating = rating,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context!.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        return user;
    }

    // 테스트용 TradeGear 생성
    private async Task<TradeGear> CreateTestGearAsync(
        Guid authorId,
        string title = "Test Gear",
        string description = "Test description",
        int price = 100000,
        GearCategory category = GearCategory.Instrument,
        GearSubCategory subCategory = GearSubCategory.Guitar,
        GearDetailCategory detailCategory = GearDetailCategory.Electric,
        GearCondition condition = GearCondition.Good,
        TradeMethod tradeMethod = TradeMethod.Direct,
        Region region = Region.Seoul,
        int viewCount = 0,
        int likeCount = 0,
        ImageData? images = null)
    {
        var gear = new TradeGear
        {
            Title = title,
            Description = description,
            Price = price,
            Images = images,
            Category = category,
            SubCategory = subCategory,
            DetailCategory = detailCategory,
            Condition = condition,
            TradeMethod = tradeMethod,
            Region = region,
            Status = GearStatus.Selling,
            ViewCount = viewCount,
            LikeCount = likeCount,
            AuthorId = authorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context!.TradeGears.AddAsync(gear);
        await _context.SaveChangesAsync();
        return gear;
    }

    // **************************************

    #region 악기거래 게시글 작성 테스트

    // 정상 작성
    // 성공: 공개 이미지 베이스 URL이 이미지 URL에 붙는지 확인
    [Fact]
    public async Task CreateGearAsync_ValidRequest_Success()
    {
        // Arrange
        InitializeContext();

        // 테스트용 User 생성 및 저장
        var userId = Guid.NewGuid();
        var testUser = new api.Models.User
        {
            Id = userId,
            Email = "test@test.com",
            Nickname = "testuser",
            Password = "hashed_password",
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context!.Users.AddAsync(testUser);
        await _context.SaveChangesAsync();

        var request = new CreateGearRequest
        {
            Title = "Test Gear",
            Description = "This is a test gear description.",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul,
        };

        // 이미지 준비
        var images = new List<Stream>
        {
            new MemoryStream(new byte[] { 0x20, 0x20, 0x20 }) // 더미 이미지 데이터
        };

        // ImageService Mock
        var expectedImageUrls = new List<string> { "gears/test-image-1.jpg" };
        _imageServiceMock
            .Setup(x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), "gears"))
            .ReturnsAsync(expectedImageUrls);

        // Act
        var response = await _tradeGearService.CreateGearAsync(userId, request, images);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(request.Title, response.Title);
        Assert.Equal(request.Description, response.Description);
        Assert.Equal(request.Price, response.Price);
        Assert.Equal(request.Category, response.Category);
        Assert.Equal(request.SubCategory, response.SubCategory);
        Assert.Equal(request.DetailCategory, response.DetailCategory);
        Assert.Equal(request.Condition, response.Condition);
        Assert.Equal(request.TradeMethod, response.TradeMethod);
        Assert.Equal(request.Region, response.Region);
        Assert.Equal(GearStatus.Selling, response.Status);
        Assert.Equal(0, response.ViewCount);
        Assert.Equal(0, response.LikeCount);
        Assert.Equal(userId, response.AuthorId);
        Assert.Equal(testUser.Nickname, response.AuthorNickname);
        Assert.NotNull(response.Images);
        Assert.Equal(1, response.Images.Count); // 이미지 개수가 1개
        Assert.Equal(expectedImageUrls[0], response.Images.Urls[0]); // json의 url이 잘 담겨있는지 확인

        // DB에 Gear가 생성되었는지 확인
        var createdGear = await _context.TradeGears.FirstOrDefaultAsync(g => g.Title == request.Title);
        Assert.NotNull(createdGear);
        Assert.Equal(userId, createdGear.AuthorId);
        Assert.Null(createdGear.DeletedAt);

        // ImageService가 호출되었는지 확인
        _imageServiceMock.Verify(
            x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), "gears"),
            Times.Once
        );
    }

    // 지정한 MainImageIndex가 이미지 데이터에 반영되는지 확인
    [Fact]
    public async Task CreateGearAsync_MainImageIndex_SetsMainIndex()
    {
        // Arrange
        InitializeContext();

        var user = await CreateTestUserAsync();

        var request = new CreateGearRequest
        {
            Title = "Main Index Gear",
            Description = "Representative image index should be stored.",
            Price = 120000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul,
            MainImageIndex = 2
        };

        var images = new List<Stream>
        {
            new MemoryStream(new byte[] { 0x01 }),
            new MemoryStream(new byte[] { 0x02 }),
            new MemoryStream(new byte[] { 0x03 })
        };

        var expectedImageUrls = new List<string>
        {
            "gears/image-1.jpg",
            "gears/image-2.jpg",
            "gears/image-3.jpg"
        };

        _imageServiceMock
            .Setup(x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), "gears"))
            .ReturnsAsync(expectedImageUrls);

        // Act
        var response = await _tradeGearService.CreateGearAsync(user.Id, request, images);

        // Assert
        Assert.NotNull(response.Images);
        Assert.Equal(2, response.Images.MainIndex);

        var createdGear = await _context!.TradeGears.FirstOrDefaultAsync(g => g.Title == request.Title);
        Assert.NotNull(createdGear);
        Assert.NotNull(createdGear.Images);
        Assert.Equal(2, createdGear.Images.MainIndex);
    }

    // 실패: 이미지 여러 장 중 일부만 업로드되면 예외를 던지고 글이 생성되지 않는지 확인하는 테스트
    [Fact]
    public async Task CreateGearAsync_ImageUploadPartialFailure_Failure()
    {
        // Arrange
        InitializeContext();

        var user = await CreateTestUserAsync();

        var request = new CreateGearRequest
        {
            Title = "Partial Upload",
            Description = "Images should all upload or fail.",
            Price = 90000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var images = new List<Stream>
        {
            new MemoryStream(new byte[] { 0x01 }),
            new MemoryStream(new byte[] { 0x02 })
        };

        var uploadedUrls = new List<string> { "gears/image-1.jpg" };

        _imageServiceMock
            .Setup(x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), "gears"))
            .ReturnsAsync(uploadedUrls);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _tradeGearService.CreateGearAsync(user.Id, request, images)
        );
        Assert.Equal("Failed to upload all images", exception.Message);

        var createdGear = await _context!.TradeGears.FirstOrDefaultAsync(g => g.Title == request.Title);
        Assert.Null(createdGear);
    }

    // 실패: 이미지 없이 작성 시도
    [Fact]
    public async Task CreateGearAsync_NoImages_Failure()
    {
        // Arrange
        InitializeContext();

        var userId = Guid.NewGuid();
        var testUser = new User
        {
            Id = userId,
            Email = "test@test.com",
            Nickname = "testuser",
            Password = "hashed_password",
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context!.Users.AddAsync(testUser);
        await _context.SaveChangesAsync();

        var request = new CreateGearRequest
        {
            Title = "Test Gear",
            Description = "This is a test gear description.",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul,
        };

        // Act & Assert - 이미지 없이 작성 시도
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _tradeGearService.CreateGearAsync(userId, request, imageStreams: null)
        );
        Assert.Equal("At least one image is required", exception.Message);

        // DB에 Gear가 생성되지 않았는지 확인
        var createdGear = await _context!.TradeGears.FirstOrDefaultAsync(g => g.Title == request.Title);
        Assert.Null(createdGear);
    }

    // 실패: 로그인된 사용자가 아닌 경우
    [Fact]
    public async Task CreateGearAsync_InvalidUser_Failure()
    {
        // Arrange
        InitializeContext();

        // 테스트용 User ID (존재하지 않는 사용자)
        var userId = Guid.NewGuid();

        var request = new CreateGearRequest
        {
            Title = "Test Gear",
            Description = "This is a test gear description.",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul,
        };

        // 이미지 준비
        var images = new List<Stream>
        {
            new MemoryStream(new byte[] { 0x20, 0x20, 0x20 }) // 더미 이미지 데이터
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _tradeGearService.CreateGearAsync(userId, request, images)
        );
        Assert.Equal("Author not found", exception.Message);

        // DB에 Gear가 생성되지 않았는지 확인
        var createdGear = await _context!.TradeGears.FirstOrDefaultAsync(g => g.Title == request.Title);
        Assert.Null(createdGear);

        // ImageService가 호출되지 않아야 함
        _imageServiceMock.Verify(
            x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), "gears"),
            Times.Never
        );
    }

    #endregion

    #region 악기거래 게시글 목록 조회 테스트

    // 정상 조회
    [Fact]
    public async Task GetGearsAsync_NoFilter_Success()
    {
        // Arrange
        InitializeContext();

        var author1 = await CreateTestUserAsync(email: "author1@test.com", nickname: "author1");
        var author2 = await CreateTestUserAsync(email: "author2@test.com", nickname: "author2");

        var imageData = new ImageData
        {
            Count = 1,
            Urls = ["gears/test-image.jpg"]
        };

        // 여러 개의 게시글 생성
        await CreateTestGearAsync(author1.Id, title: "Guitar 1", price: 100000, category: GearCategory.Instrument, images: imageData);
        await CreateTestGearAsync(
            author2.Id,
            title: "Bass 1",
            price: 200000,
            category: GearCategory.Instrument,
            subCategory: GearSubCategory.Bass,
            detailCategory: GearDetailCategory.ElectricBass,
            images: imageData);
        await CreateTestGearAsync(author1.Id, title: "Guitar 2", price: 150000, category: GearCategory.Instrument, images: imageData);

        var request = new GetGearsRequest
        {
            Page = 1,
            PageSize = 10,
            SortBy = "created_at",
            SortOrder = "desc"
        };

        // Act
        var response = await _tradeGearService.GetGearsAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(3, response.TotalCount);
        Assert.Equal(3, response.Gears.Count);
        Assert.Equal(1, response.Page);
        Assert.Equal(10, response.PageSize);
        Assert.Equal(1, response.TotalPages);
    }

    // Category 필터링
    [Fact]
    public async Task GetGearsAsync_FilterByCategory_Success()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();
        var imageData = new ImageData
        {
            Count = 1,
            Urls = ["gears/test-image.jpg"]
        };

        await CreateTestGearAsync(author.Id, title: "Guitar 1", category: GearCategory.Instrument, images: imageData);
        await CreateTestGearAsync(
            author.Id,
            title: "Bass 1",
            category: GearCategory.Instrument,
            subCategory: GearSubCategory.Bass,
            detailCategory: GearDetailCategory.ElectricBass,
            images: imageData);
        await CreateTestGearAsync(author.Id, title: "Guitar 2", category: GearCategory.Instrument, images: imageData);
        await CreateTestGearAsync(
            author.Id,
            title: "Drum 1",
            category: GearCategory.Instrument,
            subCategory: GearSubCategory.Drum,
            detailCategory: GearDetailCategory.AcousticDrum,
            images: imageData);

        var request = new GetGearsRequest
        {
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            Page = 1,
            PageSize = 10
        };

        // Act
        var response = await _tradeGearService.GetGearsAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(2, response.TotalCount);
        Assert.Equal(2, response.Gears.Count);
        Assert.All(response.Gears, g =>
        {
            Assert.Equal(GearCategory.Instrument, g.Category);
            Assert.Equal(GearSubCategory.Guitar, g.SubCategory);
        });
    }

    // 가격 범위 필터링
    [Fact]
    public async Task GetGearsAsync_FilterByPriceRange_Success()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();
        var imageData = new ImageData
        {
            Count = 1,
            Urls = ["gears/test-image.jpg"]
        };

        await CreateTestGearAsync(author.Id, title: "Cheap", price: 50000, images: imageData);
        await CreateTestGearAsync(author.Id, title: "Mid 1", price: 150000, images: imageData);
        await CreateTestGearAsync(author.Id, title: "Mid 2", price: 200000, images: imageData);
        await CreateTestGearAsync(author.Id, title: "Expensive", price: 500000, images: imageData);

        var request = new GetGearsRequest
        {
            MinPrice = 100000,
            MaxPrice = 300000,
            Page = 1,
            PageSize = 10
        };

        // Act
        var response = await _tradeGearService.GetGearsAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(2, response.TotalCount);
        Assert.Equal(2, response.Gears.Count);
        Assert.All(response.Gears, g =>
        {
            Assert.True(g.Price >= 100000);
            Assert.True(g.Price <= 300000);
        });
    }

    // 검색 키워드 필터링
    [Fact]
    public async Task GetGearsAsync_FilterBySearchKeyword_Success()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();
        var imageData = new ImageData
        {
            Count = 1,
            Urls = ["gears/test-image.jpg"]
        };

        await CreateTestGearAsync(author.Id, title: "Fender Stratocaster", description: "Electric guitar", images: imageData);
        await CreateTestGearAsync(author.Id, title: "Gibson Les Paul", description: "Classic guitar", images: imageData);
        await CreateTestGearAsync(
            author.Id,
            title: "Yamaha Bass",
            description: "Bass guitar",
            subCategory: GearSubCategory.Bass,
            detailCategory: GearDetailCategory.ElectricBass,
            images: imageData);

        var request = new GetGearsRequest
        {
            SearchKeyword = "fender",
            Page = 1,
            PageSize = 10
        };

        // Act
        var response = await _tradeGearService.GetGearsAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(1, response.TotalCount);
        Assert.Single(response.Gears);
        Assert.Contains("Fender", response.Gears[0].Title);
    }

    // 가격 정렬 (오름차순)
    [Fact]
    public async Task GetGearsAsync_SortByPriceAsc_Success()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();
        var imageData = new ImageData
        {
            Count = 1,
            Urls = ["gears/test-image.jpg"]
        };

        await CreateTestGearAsync(author.Id, title: "Gear 1", price: 300000, images: imageData);
        await CreateTestGearAsync(author.Id, title: "Gear 2", price: 100000, images: imageData);
        await CreateTestGearAsync(author.Id, title: "Gear 3", price: 200000, images: imageData);

        var request = new GetGearsRequest
        {
            Page = 1,
            PageSize = 10,
            SortBy = "price",
            SortOrder = "asc"
        };

        // Act
        var response = await _tradeGearService.GetGearsAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(3, response.TotalCount);
        Assert.Equal(100000, response.Gears[0].Price);
        Assert.Equal(200000, response.Gears[1].Price);
        Assert.Equal(300000, response.Gears[2].Price);
    }

    // 페이징
    [Fact]
    public async Task GetGearsAsync_Pagination_Success()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();
        var imageData = new ImageData
        {
            Count = 1,
            Urls = ["gears/test-image.jpg"]
        };

        // 5개의 게시글 생성
        for (int i = 1; i <= 5; i++)
        {
            await CreateTestGearAsync(author.Id, title: $"Gear {i}", images: imageData);
        }

        var request = new GetGearsRequest
        {
            Page = 2,
            PageSize = 2,
            SortBy = "created_at",
            SortOrder = "asc"
        };

        // Act
        var response = await _tradeGearService.GetGearsAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(5, response.TotalCount);
        Assert.Equal(2, response.Gears.Count);
        Assert.Equal(2, response.Page);
        Assert.Equal(2, response.PageSize);
        Assert.Equal(3, response.TotalPages);
    }

    // Soft delete된 게시글 제외
    [Fact]
    public async Task GetGearsAsync_ExcludeSoftDeleted_Success()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();
        var imageData = new ImageData
        {
            Count = 1,
            Urls = ["gears/test-image.jpg"]
        };

        var gear1 = await CreateTestGearAsync(author.Id, title: "Active Gear", images: imageData);
        var gear2 = await CreateTestGearAsync(author.Id, title: "Deleted Gear", images: imageData);
        var gear3 = await CreateTestGearAsync(author.Id, title: "Another Active", images: imageData);

        // gear2 삭제
        await _tradeGearService.DeleteGearAsync(gear2.Id, author.Id);

        var request = new GetGearsRequest
        {
            Page = 1,
            PageSize = 10
        };

        // Act
        var response = await _tradeGearService.GetGearsAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(2, response.TotalCount); // 삭제된 것 제외하고 2개만
        Assert.Equal(2, response.Gears.Count);
        Assert.DoesNotContain(response.Gears, g => g.Id == gear2.Id);
    }

    // 복합 필터링 (Category + Price + Keyword)
    [Fact]
    public async Task GetGearsAsync_MultipleFilters_Success()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();
        var imageData = new ImageData
        {
            Count = 1,
            Urls = ["gears/test-image.jpg"]
        };

        await CreateTestGearAsync(author.Id, title: "Fender Guitar", price: 150000, category: GearCategory.Instrument, images: imageData);
        await CreateTestGearAsync(author.Id, title: "Gibson Guitar", price: 250000, category: GearCategory.Instrument, images: imageData);
        await CreateTestGearAsync(
            author.Id,
            title: "Fender Bass",
            price: 180000,
            category: GearCategory.Instrument,
            subCategory: GearSubCategory.Bass,
            detailCategory: GearDetailCategory.ElectricBass,
            images: imageData);
        await CreateTestGearAsync(author.Id, title: "Cheap Guitar", price: 50000, category: GearCategory.Instrument, images: imageData);

        var request = new GetGearsRequest
        {
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            MinPrice = 100000,
            SearchKeyword = "fender",
            Page = 1,
            PageSize = 10
        };

        // Act
        var response = await _tradeGearService.GetGearsAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(1, response.TotalCount);
        Assert.Single(response.Gears);
        Assert.Equal("Fender Guitar", response.Gears[0].Title);
        Assert.Equal(GearCategory.Instrument, response.Gears[0].Category);
        Assert.True(response.Gears[0].Price >= 100000);
    }

    // 공개 이미지 url 생성
    [Fact]
    public async Task GetGearsAsync_PublicImageBaseUrl_PrependsUrls()
    {
        // Arrange
        var baseUrl = "https://cdn.example.com/bucket";
        Environment.SetEnvironmentVariable("S3_PUBLIC_BASE_URL", baseUrl);
        InitializeContext();
        var publicImageUrlService = new PublicImageUrlService();
        _publicImageUrlServiceMock
            .Setup(x => x.ToPublicImageData(It.IsAny<ImageData?>()))
            .Returns<ImageData?>(images => publicImageUrlService.ToPublicImageData(images));

        var author = await CreateTestUserAsync();
        var imageData = new ImageData
        {
            Count = 1,
            Urls = ["gears/test-image.jpg"],
            MainIndex = 0
        };

        await CreateTestGearAsync(author.Id, title: "Public URL Gear", images: imageData);

        var request = new GetGearsRequest
        {
            Page = 1,
            PageSize = 10
        };

        // Act
        var response = await _tradeGearService.GetGearsAsync(request);

        // Assert
        Assert.Single(response.Gears);
        var images = response.Gears[0].Images;
        Assert.NotNull(images);
        Assert.Equal($"{baseUrl}/gears/test-image.jpg", images!.Urls[0]);

        Environment.SetEnvironmentVariable("S3_PUBLIC_BASE_URL", null);
    }

    #endregion

    #region 악기거래 게시글 상세 조회 테스트

    // 정상 조회
    [Fact]
    public async Task GetGearByIdAsync_ValidRequest_Success()
    {
        // Arrange
        InitializeContext();
        var author = await CreateTestUserAsync();

        // 이미지 데이터 준비
        var imageData = new ImageData
        {
            Count = 2,
            Urls = new List<string>
            {
                "gears/test-image-1.jpg",
                "gears/test-image-2.jpg"
            }
        };

        // 테스트용 TradeGear 생성
        var gear = await CreateTestGearAsync(
            authorId: author.Id,
            title: "Fender Stratocaster",
            description: "Excellent condition electric guitar.",
            price: 150000,
            condition: GearCondition.LikeNew,
            tradeMethod: TradeMethod.Both,
            viewCount: 10,
            likeCount: 5,
            images: imageData
        );

        // Act
        var response = await _tradeGearService.GetGearByIdAsync(gear.Id);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(gear.Id, response.Id);
        Assert.Equal(gear.Title, response.Title);
        Assert.Equal(gear.Description, response.Description);
        Assert.Equal(gear.Price, response.Price);
        Assert.Equal(gear.Category, response.Category);
        Assert.Equal(gear.SubCategory, response.SubCategory);
        Assert.Equal(gear.DetailCategory, response.DetailCategory);
        Assert.Equal(gear.Condition, response.Condition);
        Assert.Equal(gear.TradeMethod, response.TradeMethod);
        Assert.Equal(gear.Region, response.Region);
        Assert.Equal(GearStatus.Selling, response.Status);
        Assert.Equal(10, response.ViewCount);
        Assert.Equal(5, response.LikeCount);
        Assert.Equal(author.Id, response.AuthorId);
        Assert.Equal(author.Nickname, response.AuthorNickname);
        Assert.Equal(author.Rating, response.AuthorRating);

        // 이미지 검증
        Assert.NotNull(response.Images);
        Assert.Equal(2, response.Images.Count);
        Assert.Equal(2, response.Images.Urls.Count);
        Assert.Equal("gears/test-image-1.jpg", response.Images.Urls[0]);
        Assert.Equal("gears/test-image-2.jpg", response.Images.Urls[1]);

        // 로그인하지 않은 상태이므로 IsLiked와 IsAuthor는 null
        Assert.Null(response.IsLiked);
        Assert.Null(response.IsAuthor);
    }

    // 실패: 없는 게시글
    [Fact]
    public async Task GetGearByIdAsync_InvalidId_Failure()
    {
        // Arrange
        InitializeContext();

        long invalidId = 999999; // 존재하지 않는 ID

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _tradeGearService.GetGearByIdAsync(invalidId)
        );

        Assert.Equal("Gear not found", exception.Message);
    }

    // 조회 시 조회수 증가
    [Fact]
    public async Task GetGearByIdAsync_IncrementViews_Success()
    {
        // Arrange
        InitializeContext();
        var author = await CreateTestUserAsync();
        var gear = await CreateTestGearAsync(author.Id, title: "테스트", description: "테스트 설명");

        var viewerUserId = Guid.NewGuid();
        var ipAddress = IPAddress.Parse("192.168.1.1");

        // Act
        var response = await _tradeGearService.GetGearByIdAsync(gear.Id, incrementViews: true, userId: viewerUserId, ipAddress: ipAddress);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(1, response.ViewCount); // 조회수가 1 증가

        // DB에서 ViewCount 확인
        var updatedGear = await _context!.TradeGears.FindAsync(gear.Id);
        Assert.NotNull(updatedGear);
        Assert.Equal(1, updatedGear.ViewCount);

        // TradeGearView 레코드가 생성되었는지 확인
        var viewRecord = await _context.TradeGearViews.FirstOrDefaultAsync(v => v.GearId == gear.Id);
        Assert.NotNull(viewRecord);
        Assert.Equal(viewerUserId, viewRecord.UserId);
        Assert.Equal(ipAddress, viewRecord.IpAddress);
    }

    // 동일 사용자 재방문 시 조회수 유지
    [Fact]
    public async Task GetGearByIdAsync_DuplicateView_ViewCountNotIncreased()
    {
        // Arrange
        InitializeContext();
        var author = await CreateTestUserAsync();
        var gear = await CreateTestGearAsync(author.Id);

        var viewerUserId = Guid.NewGuid();
        var ipAddress = IPAddress.Parse("192.168.1.1");

        // Act
        // 첫 번째 조회
        var response1 = await _tradeGearService.GetGearByIdAsync(gear.Id, incrementViews: true, userId: viewerUserId, ipAddress: ipAddress);
        Assert.Equal(1, response1.ViewCount);

        // 두 번째 조회 (30분 이내, 같은 유저/IP)
        var response2 = await _tradeGearService.GetGearByIdAsync(gear.Id, incrementViews: true, userId: viewerUserId, ipAddress: ipAddress);

        // Assert
        Assert.NotNull(response2);
        Assert.Equal(1, response2.ViewCount); // 조회수 유지

        // DB에서 ViewCount 확인
        var updatedGear = await _context!.TradeGears.FindAsync(gear.Id);
        Assert.NotNull(updatedGear);
        Assert.Equal(1, updatedGear.ViewCount);

        // TradeGearView 레코드가 1개만 있는지 확인
        var viewRecords = await _context.TradeGearViews.Where(v => v.GearId == gear.Id).ToListAsync();
        Assert.Single(viewRecords);
    }

    #endregion

    #region 악기거래 게시글 수정 테스트

    // 정상 수정 (필드 업데이트 + 이미지 추가)
    [Fact]
    public async Task UpdateGearAsync_ValidRequest_Success()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();

        var existingImageData = new ImageData
        {
            Count = 1,
            Urls = new List<string> { "gears/existing-1.jpg" }
        };

        var gear = await CreateTestGearAsync(
            authorId: author.Id,
            viewCount: 10,
            likeCount: 5,
            images: existingImageData
        );

        var originalUpdatedAt = gear.UpdatedAt;
        var originalViewCount = gear.ViewCount;
        var originalLikeCount = gear.LikeCount;

        var request = new UpdateGearRequest
        {
            Title = "Updated Title",
            Description = "Updated Description",
            Price = 200000,
            Condition = GearCondition.Fair,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Bass,
            DetailCategory = GearDetailCategory.ElectricBass,
            TradeMethod = TradeMethod.Both,
            Region = Region.Busan,
            KeepImageUrls = new List<string> { "gears/existing-1.jpg" } // 기존 이미지 유지
        };

        var newImages = new List<Stream>
        {
            new MemoryStream(new byte[] { 0x01, 0x02, 0x03 }),
            new MemoryStream(new byte[] { 0x04, 0x05, 0x06 })
        };

        var expectedImageUrls = new List<string>
        {
            "gears/new-image-1.jpg",
            "gears/new-image-2.jpg"
        };

        _imageServiceMock
            .Setup(x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), "gears"))
            .ReturnsAsync(expectedImageUrls);

        // Act
        var response = await _tradeGearService.UpdateGearAsync(gear.Id, author.Id, request, newImages);

        // Assert
        // Response 검증
        Assert.NotNull(response);
        Assert.Equal(request.Title, response.Title);
        Assert.Equal(request.Description, response.Description);
        Assert.Equal(request.Price, response.Price);
        Assert.Equal(request.Category, response.Category);
        Assert.Equal(request.SubCategory, response.SubCategory);
        Assert.Equal(request.DetailCategory, response.DetailCategory);
        Assert.Equal(request.Condition, response.Condition);
        Assert.Equal(request.TradeMethod, response.TradeMethod);
        Assert.Equal(request.Region, response.Region);

        // 변경되지 않아야 할 필드들 검증
        Assert.Equal(GearStatus.Selling, response.Status);
        Assert.Equal(originalViewCount, response.ViewCount);
        Assert.Equal(originalLikeCount, response.LikeCount);
        Assert.Equal(author.Id, response.AuthorId);
        Assert.True(response.UpdatedAt > originalUpdatedAt);

        // 이미지 검증
        Assert.NotNull(response.Images);
        Assert.Equal(3, response.Images.Count);
        Assert.Equal("gears/existing-1.jpg", response.Images.Urls[0]); // 기존 이미지
        Assert.Equal(expectedImageUrls[0], response.Images.Urls[1]); // 새 이미지
        Assert.Equal(expectedImageUrls[1], response.Images.Urls[2]); // 새 이미지

        // DB 검증
        var updatedGear = await _context!.TradeGears.FirstOrDefaultAsync(g => g.Id == gear.Id);
        Assert.NotNull(updatedGear);
        Assert.Equal(request.Title, updatedGear.Title);
        Assert.Equal(request.Description, updatedGear.Description);
        Assert.Equal(request.Price, updatedGear.Price);
        Assert.Equal(request.Category, updatedGear.Category);
        Assert.Equal(request.SubCategory, updatedGear.SubCategory);
        Assert.Equal(request.DetailCategory, updatedGear.DetailCategory);
        Assert.Equal(request.Condition, updatedGear.Condition);
        Assert.Equal(request.TradeMethod, updatedGear.TradeMethod);
        Assert.Equal(request.Region, updatedGear.Region);
        Assert.Equal(GearStatus.Selling, updatedGear.Status);
        Assert.Equal(originalViewCount, updatedGear.ViewCount);
        Assert.Equal(originalLikeCount, updatedGear.LikeCount);
        Assert.True(updatedGear.UpdatedAt > originalUpdatedAt);
        Assert.Null(updatedGear.DeletedAt);
        Assert.NotNull(updatedGear.Images);
        Assert.Equal(3, updatedGear.Images.Count);

        // ImageService 호출 확인 (DeleteImages는 호출되지 않아야 함 - 모두 유지)
        _imageServiceMock.Verify(
            x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), "gears"),
            Times.Once
        );
        _imageServiceMock.Verify(
            x => x.DeleteImagesAsync(It.IsAny<List<string>?>()),
            Times.Never
        );
    }

    // 대표 이미지 변경 수정
    [Fact]
    public async Task UpdateGearAsync_MainImageIndex_UpdatesMainIndex()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();

        var existingImageData = new ImageData
        {
            Count = 3,
            Urls = ["gears/existing-1.jpg", "gears/existing-2.jpg", "gears/existing-3.jpg"],
            MainIndex = 0
        };

        var gear = await CreateTestGearAsync(
            authorId: author.Id,
            images: existingImageData
        );

        var request = new UpdateGearRequest
        {
            MainImageIndex = 2
        };

        // Act
        var response = await _tradeGearService.UpdateGearAsync(gear.Id, author.Id, request);

        // Assert
        Assert.NotNull(response.Images);
        Assert.Equal(2, response.Images.MainIndex);

        var updatedGear = await _context!.TradeGears.FirstOrDefaultAsync(g => g.Id == gear.Id);
        Assert.NotNull(updatedGear);
        Assert.NotNull(updatedGear.Images);
        Assert.Equal(2, updatedGear.Images.MainIndex);
    }

    // 실패: 없는 글
    [Fact]
    public async Task UpdateGearAsync_InvalidGearIdRequest_Failure()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();
        long InvalidGearId = 999999;

        var request = new UpdateGearRequest
        {
            Title = "Updated Title",
            Description = "Updated Description",
            Price = 200000,
            Condition = GearCondition.Fair,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Bass,
            DetailCategory = GearDetailCategory.ElectricBass,
            TradeMethod = TradeMethod.Both,
            Region = Region.Busan
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _tradeGearService.UpdateGearAsync(InvalidGearId, author.Id, request)
        );

        Assert.Equal("Gear not found", exception.Message);

        // DB에 해당 Gear ID가 존재하지 않는지 다시 확인(혹시나 만들어졌을까봐)
        var createdGear = await _context!.TradeGears.FirstOrDefaultAsync(g => g.Id == InvalidGearId);
        Assert.Null(createdGear);
    }

    // 실패: 작성자가 아닌 사용자의 수정 시도
    [Fact]
    public async Task UpdateGearAsync_InvalidAuthorIdRequest_Failure()
    {
        // Arrange
        InitializeContext();

        Guid InvalidAuthorId = Guid.NewGuid();
        var author = await CreateTestUserAsync();
        var gear = await CreateTestGearAsync(
            authorId: author.Id,
            viewCount: 10,
            likeCount: 5
        );

        var request = new UpdateGearRequest
        {
            Title = "Updated Title",
            Description = "Updated Description",
            Price = 200000,
            Condition = GearCondition.Fair,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Bass,
            DetailCategory = GearDetailCategory.ElectricBass,
            TradeMethod = TradeMethod.Both,
            Region = Region.Busan
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _tradeGearService.UpdateGearAsync(gear.Id, InvalidAuthorId, request)
        );

        Assert.Equal("You are not authorized to update this gear", exception.Message);

        // DB에 반영되지 않았는지 확인
        var updatedGear = await _context!.TradeGears.FirstOrDefaultAsync(g => g.Id == gear.Id);
        Assert.NotNull(updatedGear);
        Assert.Equal(gear.UpdatedAt, updatedGear.UpdatedAt);
    }

    // 기존 이미지를 전부 삭제하고 새 이미지로 교체
    [Fact]
    public async Task UpdateGearAsync_ReplaceAllImages_Success()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();

        var oldImageData = new ImageData
        {
            Count = 2,
            Urls = new List<string>
            {
                "gears/old-image-1.jpg",
                "gears/old-image-2.jpg"
            }
        };

        var gear = await CreateTestGearAsync(authorId: author.Id, images: oldImageData);

        var request = new UpdateGearRequest
        {
            Title = "Updated with New Images",
            KeepImageUrls = new List<string>() // 기존 이미지 모두 삭제
        };

        var newImages = new List<Stream>
        {
            new MemoryStream(new byte[] { 0x10, 0x20, 0x30 })
        };

        var newImageUrls = new List<string> { "gears/new-image.jpg" };

        _imageServiceMock
            .Setup(x => x.DeleteImagesAsync(It.Is<List<string>>(urls =>
                urls.Count == 2 &&
                urls.Contains("gears/old-image-1.jpg") &&
                urls.Contains("gears/old-image-2.jpg"))))
            .ReturnsAsync(2);

        _imageServiceMock
            .Setup(x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), "gears"))
            .ReturnsAsync(newImageUrls);

        // Act
        var response = await _tradeGearService.UpdateGearAsync(gear.Id, author.Id, request, newImages);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Updated with New Images", response.Title);
        Assert.NotNull(response.Images);
        Assert.Equal(1, response.Images.Count);
        Assert.Equal("gears/new-image.jpg", response.Images.Urls[0]);

        // DB 검증
        var updatedGear = await _context!.TradeGears.FirstOrDefaultAsync(g => g.Id == gear.Id);
        Assert.NotNull(updatedGear);
        Assert.NotNull(updatedGear.Images);
        Assert.Equal(1, updatedGear.Images.Count);
        Assert.Equal("gears/new-image.jpg", updatedGear.Images.Urls[0]);

        // 기존 이미지 전부 삭제 확인
        _imageServiceMock.Verify(
            x => x.DeleteImagesAsync(It.Is<List<string>>(urls =>
                urls.Count == 2 &&
                urls.Contains("gears/old-image-1.jpg") &&
                urls.Contains("gears/old-image-2.jpg"))),
            Times.Once
        );

        // 새 이미지 업로드 확인
        _imageServiceMock.Verify(
            x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), "gears"),
            Times.Once
        );
    }

    // 이미지 파라미터 null로 전달 시 기존 이미지 유지 (KeepImageUrls도 null)
    [Fact]
    public async Task UpdateGearAsync_NullImages_KeepExistingImages()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();

        var existingImageData = new ImageData
        {
            Count = 2,
            Urls = new List<string>
            {
                "gears/existing-1.jpg",
                "gears/existing-2.jpg"
            }
        };

        var gear = await CreateTestGearAsync(authorId: author.Id, images: existingImageData);

        var request = new UpdateGearRequest
        {
            Title = "Updated Title Only",
            KeepImageUrls = null // null이면 이미지 변경 안 함
        };

        // Act
        // imageStreams도 null로 전달
        var response = await _tradeGearService.UpdateGearAsync(gear.Id, author.Id, request, imageStreams: null);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Updated Title Only", response.Title);
        Assert.NotNull(response.Images);
        Assert.Equal(2, response.Images.Count);
        Assert.Equal("gears/existing-1.jpg", response.Images.Urls[0]);
        Assert.Equal("gears/existing-2.jpg", response.Images.Urls[1]);

        // DB 검증 - 기존 이미지가 그대로 유지되어야 함
        var updatedGear = await _context!.TradeGears.FirstOrDefaultAsync(g => g.Id == gear.Id);
        Assert.NotNull(updatedGear);
        Assert.NotNull(updatedGear.Images);
        Assert.Equal(2, updatedGear.Images.Count);
        Assert.Equal("gears/existing-1.jpg", updatedGear.Images.Urls[0]);
        Assert.Equal("gears/existing-2.jpg", updatedGear.Images.Urls[1]);

        // 이미지 서비스가 호출되지 않았는지 확인
        _imageServiceMock.Verify(
            x => x.DeleteImagesAsync(It.IsAny<List<string>?>()),
            Times.Never
        );
        _imageServiceMock.Verify(
            x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), It.IsAny<string>()),
            Times.Never
        );
    }

    // 일부 이미지만 유지하고 나머지 삭제 + 새 이미지 추가
    [Fact]
    public async Task UpdateGearAsync_KeepSomeImagesAndAddNew_Success()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();

        var existingImageData = new ImageData
        {
            Count = 3,
            Urls = new List<string>
            {
                "gears/image-A.jpg",
                "gears/image-B.jpg",
                "gears/image-C.jpg"
            }
        };

        var gear = await CreateTestGearAsync(authorId: author.Id, images: existingImageData);

        var request = new UpdateGearRequest
        {
            Title = "Partial Update",
            KeepImageUrls = new List<string>
            {
                "gears/image-B.jpg" // B만 유지, A와 C는 삭제
            }
        };

        var newImages = new List<Stream>
        {
            new MemoryStream(new byte[] { 0xFF, 0xFF })
        };

        var newImageUrls = new List<string> { "gears/image-D.jpg" };

        _imageServiceMock
            .Setup(x => x.DeleteImagesAsync(It.Is<List<string>>(urls =>
                urls.Count == 2 &&
                urls.Contains("gears/image-A.jpg") &&
                urls.Contains("gears/image-C.jpg"))))
            .ReturnsAsync(2);

        _imageServiceMock
            .Setup(x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), "gears"))
            .ReturnsAsync(newImageUrls);

        // Act
        var response = await _tradeGearService.UpdateGearAsync(gear.Id, author.Id, request, newImages);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Partial Update", response.Title);
        Assert.NotNull(response.Images);
        Assert.Equal(2, response.Images.Count);
        Assert.Equal("gears/image-B.jpg", response.Images.Urls[0]); // 유지한 이미지
        Assert.Equal("gears/image-D.jpg", response.Images.Urls[1]); // 새 이미지

        // DB 검증
        var updatedGear = await _context!.TradeGears.FirstOrDefaultAsync(g => g.Id == gear.Id);
        Assert.NotNull(updatedGear);
        Assert.NotNull(updatedGear.Images);
        Assert.Equal(2, updatedGear.Images.Count);

        // A, C만 삭제 확인
        _imageServiceMock.Verify(
            x => x.DeleteImagesAsync(It.Is<List<string>>(urls =>
                urls.Count == 2 &&
                urls.Contains("gears/image-A.jpg") &&
                urls.Contains("gears/image-C.jpg"))),
            Times.Once
        );

        // 새 이미지 업로드 확인
        _imageServiceMock.Verify(
            x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), "gears"),
            Times.Once
        );
    }

    // 실패: 이미지 중 하나라도 업로드 되지 않음
    [Fact]
    public async Task UpdateGearAsync_ImageUploadPartialFailure_Failure()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();

        var existingImageData = new ImageData
        {
            Count = 1,
            Urls = new List<string> { "gears/existing.jpg" },
            MainIndex = 0
        };

        var gear = await CreateTestGearAsync(authorId: author.Id, images: existingImageData);

        var request = new UpdateGearRequest
        {
            Title = "Should Fail",
            KeepImageUrls = new List<string> { "gears/existing.jpg" }
        };

        var newImages = new List<Stream>
        {
            new MemoryStream(new byte[] { 0x01 }),
            new MemoryStream(new byte[] { 0x02 })
        };

        var uploadedUrls = new List<string> { "gears/new-1.jpg" };

        _imageServiceMock
            .Setup(x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), "gears"))
            .ReturnsAsync(uploadedUrls);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _tradeGearService.UpdateGearAsync(gear.Id, author.Id, request, newImages)
        );
        Assert.Equal("Failed to upload all images", exception.Message);

        var unchangedGear = await _context!.TradeGears.FirstOrDefaultAsync(g => g.Id == gear.Id);
        Assert.NotNull(unchangedGear);
        Assert.NotNull(unchangedGear.Images);
        Assert.Equal(1, unchangedGear.Images.Count);
        Assert.Equal("gears/existing.jpg", unchangedGear.Images.Urls[0]);
    }

    // 메인 이미지를 유지하면 메인 인덱스도 유지
    [Fact]
    public async Task UpdateGearAsync_PreservesMainIndex_WhenMainImageKept()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();

        var existingImageData = new ImageData
        {
            Count = 3,
            Urls = new List<string>
            {
                "gears/image-A.jpg",
                "gears/image-B.jpg",
                "gears/image-C.jpg"
            },
            MainIndex = 1
        };

        var gear = await CreateTestGearAsync(authorId: author.Id, images: existingImageData);

        var request = new UpdateGearRequest
        {
            KeepImageUrls = new List<string>
            {
                "gears/image-B.jpg",
                "gears/image-A.jpg"
            }
        };

        // Act
        var response = await _tradeGearService.UpdateGearAsync(gear.Id, author.Id, request, imageStreams: null);

        // Assert
        Assert.NotNull(response.Images);
        Assert.Equal(0, response.Images.MainIndex);
        Assert.Equal("gears/image-B.jpg", response.Images.Urls[response.Images.MainIndex]);
    }

    // 이미지 순서만 변경
    [Fact]
    public async Task UpdateGearAsync_ReorderImagesOnly_Success()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();

        var existingImageData = new ImageData
        {
            Count = 3,
            Urls = new List<string>
            {
                "gears/image-A.jpg",
                "gears/image-B.jpg",
                "gears/image-C.jpg"
            }
        };

        var gear = await CreateTestGearAsync(authorId: author.Id, images: existingImageData);

        var request = new UpdateGearRequest
        {
            Title = "Reordered Images",
            KeepImageUrls = new List<string>
            {
                "gears/image-C.jpg", // 순서 변경: C, A, B
                "gears/image-A.jpg",
                "gears/image-B.jpg"
            }
        };

        // Act - 새 이미지 없음
        var response = await _tradeGearService.UpdateGearAsync(gear.Id, author.Id, request, imageStreams: null);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Reordered Images", response.Title);
        Assert.NotNull(response.Images);
        Assert.Equal(3, response.Images.Count);
        // 순서가 변경되었는지 확인
        Assert.Equal("gears/image-C.jpg", response.Images.Urls[0]);
        Assert.Equal("gears/image-A.jpg", response.Images.Urls[1]);
        Assert.Equal("gears/image-B.jpg", response.Images.Urls[2]);

        // DB 검증
        var updatedGear = await _context!.TradeGears.FirstOrDefaultAsync(g => g.Id == gear.Id);
        Assert.NotNull(updatedGear);
        Assert.NotNull(updatedGear.Images);
        Assert.Equal(3, updatedGear.Images.Count);
        Assert.Equal("gears/image-C.jpg", updatedGear.Images.Urls[0]);
        Assert.Equal("gears/image-A.jpg", updatedGear.Images.Urls[1]);
        Assert.Equal("gears/image-B.jpg", updatedGear.Images.Urls[2]);

        // 삭제나 업로드가 없어야 함 (순서만 변경)
        _imageServiceMock.Verify(
            x => x.DeleteImagesAsync(It.IsAny<List<string>?>()),
            Times.Never
        );
        _imageServiceMock.Verify(
            x => x.UploadImagesAsync(It.IsAny<List<Stream>>(), It.IsAny<string>()),
            Times.Never
        );
    }

    // 실패: 모든 이미지 삭제 시도
    [Fact]
    public async Task UpdateGearAsync_DeleteAllImages_Failure()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();

        var existingImageData = new ImageData
        {
            Count = 2,
            Urls = new List<string>
            {
                "gears/image-1.jpg",
                "gears/image-2.jpg"
            }
        };

        var gear = await CreateTestGearAsync(authorId: author.Id, images: existingImageData);

        var request = new UpdateGearRequest
        {
            Title = "Try to Delete All Images",
            KeepImageUrls = new List<string>() // 빈 리스트 = 모든 이미지 삭제
        };

        // Act & Assert - 이미지를 0개로 만들려는 시도는 실패해야 함
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _tradeGearService.UpdateGearAsync(gear.Id, author.Id, request, imageStreams: null)
        );
        Assert.Equal("At least one image is required", exception.Message);

        // DB에서 이미지가 그대로 유지되었는지 확인 (업데이트 안 됨)
        var unchangedGear = await _context!.TradeGears.FirstOrDefaultAsync(g => g.Id == gear.Id);
        Assert.NotNull(unchangedGear);
        Assert.NotNull(unchangedGear.Images);
        Assert.Equal(2, unchangedGear.Images.Count);
        Assert.Equal("gears/image-1.jpg", unchangedGear.Images.Urls[0]);
        Assert.Equal("gears/image-2.jpg", unchangedGear.Images.Urls[1]);
    }

    #endregion

    #region 악기거래 게시글 삭제 테스트

    // 정상 삭제 (Soft Delete)
    [Fact]
    public async Task DeleteGearAsync_ValidRequest_Success()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();
        var gear = await CreateTestGearAsync(
            authorId: author.Id,
            title: "Test Gear to Delete",
            viewCount: 10,
            likeCount: 5
        );

        var originalUpdatedAt = gear.UpdatedAt;

        // Act
        var result = await _tradeGearService.DeleteGearAsync(gear.Id, author.Id);

        // Assert
        Assert.True(result);

        // DB에서 Soft Delete 확인
        var deletedGear = await _context!.TradeGears.FirstOrDefaultAsync(g => g.Id == gear.Id);
        Assert.NotNull(deletedGear);
        Assert.NotNull(deletedGear.DeletedAt); // DeletedAt이 설정되어야 함
        Assert.True(deletedGear.UpdatedAt > originalUpdatedAt); // UpdatedAt도 갱신되어야 함

        // GetGearByIdAsync로 조회 시 찾을 수 없어야 함 (DeletedAt != null 이므로)
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _tradeGearService.GetGearByIdAsync(gear.Id)
        );
        Assert.Equal("Gear not found", exception.Message);
    }

    // 실패: 없는 게시글 삭제 시도
    [Fact]
    public async Task DeleteGearAsync_InvalidGearId_Failure()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();
        long invalidGearId = 999999;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _tradeGearService.DeleteGearAsync(invalidGearId, author.Id)
        );

        Assert.Equal("Gear not found", exception.Message);
    }

    // 실패: 작성자가 아닌 사용자의 삭제 시도
    [Fact]
    public async Task DeleteGearAsync_Unauthorized_Failure()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync(email: "author@test.com", nickname: "author");
        var otherUser = await CreateTestUserAsync(email: "other@test.com", nickname: "other");

        var gear = await CreateTestGearAsync(
            authorId: author.Id,
            title: "Author's Gear"
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _tradeGearService.DeleteGearAsync(gear.Id, otherUser.Id)
        );

        Assert.Equal("You are not authorized to delete this gear", exception.Message);

        // DB에서 삭제되지 않았는지 확인
        var unchangedGear = await _context!.TradeGears.FirstOrDefaultAsync(g => g.Id == gear.Id);
        Assert.NotNull(unchangedGear);
        Assert.Null(unchangedGear.DeletedAt); // 삭제되지 않아야 함
    }

    // 실패: 이미 삭제된 게시글 재삭제 시도
    [Fact]
    public async Task DeleteGearAsync_AlreadyDeleted_Failure()
    {
        // Arrange
        InitializeContext();

        var author = await CreateTestUserAsync();
        var gear = await CreateTestGearAsync(
            authorId: author.Id,
            title: "Already Deleted Gear"
        );

        // 첫 번째 삭제
        await _tradeGearService.DeleteGearAsync(gear.Id, author.Id);

        // Act & Assert - 두 번째 삭제 시도
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _tradeGearService.DeleteGearAsync(gear.Id, author.Id)
        );

        Assert.Equal("Gear not found", exception.Message);
    }

    #endregion
}
