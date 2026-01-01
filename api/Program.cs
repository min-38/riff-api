using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Amazon.S3;
using Amazon;
using api.Data;
using api.Services;
using api.BackgroundServices;

// Npgsql DateTime 설정 - UTC를 timestamp without time zone에 쓸 수 있도록
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// 환경에 따라 다른 .env 파일 로드
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
var envFile = environment == "Production" ? ".env.prod" : ".env.dev";

if (File.Exists(envFile))
{
    Env.Load(envFile);
    Console.WriteLine($"Loaded environment variables from {envFile} (Environment: {environment})");
}
else
{
    Console.WriteLine($"Warning: {envFile} not found, using system environment variables");
}

var dbHost = Environment.GetEnvironmentVariable("DATABASE_HOST") ?? "localhost";
var dbPort = Environment.GetEnvironmentVariable("DATABASE_PORT") ?? "5432";
var dbName = Environment.GetEnvironmentVariable("DATABASE_NAME") ?? "riff";
var dbUser = Environment.GetEnvironmentVariable("DATABASE_USER") ?? "postgres";
var dbPassword = Environment.GetEnvironmentVariable("DATABASE_PASSWORD") ?? "";

var connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword};Encoding=UTF8";

// Redis 환경 변수
var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST");
var redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD");
if (string.IsNullOrEmpty(redisHost))
    throw new InvalidOperationException("REDIS_HOST environment variable is required");

// S3 환경 변수 검증
var s3BucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME");
var s3EndpointUrl = Environment.GetEnvironmentVariable("S3_ENDPOINT_URL");
var s3RegionName = Environment.GetEnvironmentVariable("S3_REGION_NAME");
var s3AccessKey = Environment.GetEnvironmentVariable("S3_ACCESS_KEY");
var s3SecretKey = Environment.GetEnvironmentVariable("S3_SECRET_KEY");
if (string.IsNullOrEmpty(s3BucketName))
    throw new InvalidOperationException("S3_BUCKET_NAME environment variable is required");
if (string.IsNullOrEmpty(s3EndpointUrl))
    throw new InvalidOperationException("S3_ENDPOINT_URL environment variable is required");
if (string.IsNullOrEmpty(s3RegionName))
    throw new InvalidOperationException("S3_REGION_NAME environment variable is required");
if (string.IsNullOrEmpty(s3AccessKey))
    throw new InvalidOperationException("S3_ACCESS_KEY environment variable is required");
if (string.IsNullOrEmpty(s3SecretKey))
    throw new InvalidOperationException("S3_SECRET_KEY environment variable is required");

// JWT 환경 변수 검증
var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
var jwtExpirationMinutes = Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES");
if (string.IsNullOrEmpty(jwtSecretKey))
    throw new InvalidOperationException("JWT_SECRET_KEY environment variable is required");
if (string.IsNullOrEmpty(jwtIssuer))
    throw new InvalidOperationException("JWT_ISSUER environment variable is required");
if (string.IsNullOrEmpty(jwtExpirationMinutes) || !int.TryParse(jwtExpirationMinutes, out _))
    throw new InvalidOperationException("JWT_EXPIRATION_MINUTES environment variable is required and must be a valid integer");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Redis 연결 (Singleton으로 등록)
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(redisHost);

    // 비밀번호 설정 (있는 경우)
    if (!string.IsNullOrEmpty(redisPassword))
    {
        configuration.Password = redisPassword;
    }

    return ConnectionMultiplexer.Connect(configuration);
});

// S3 연결 (Singleton으로 등록)
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var config = new AmazonS3Config
    {
        ServiceURL = s3EndpointUrl,
        ForcePathStyle = true,
        UseAccelerateEndpoint = false,
        UseDualstackEndpoint = false,
        AuthenticationRegion = s3RegionName,
        MaxErrorRetry = 3,
        Timeout = TimeSpan.FromSeconds(30),
    };

    var credentials = new Amazon.Runtime.BasicAWSCredentials(s3AccessKey, s3SecretKey);
    var s3Client = new AmazonS3Client(credentials, config);

    // 연결 테스트 (특정 버킷만 확인)
    try
    {
        // ListBuckets 대신 GetBucketLocation 사용
        var locationTask = s3Client.GetBucketLocationAsync(s3BucketName);
        locationTask.Wait();

        var location = locationTask.Result.Location;
        Console.WriteLine($"S3 connection successful: bucket '{s3BucketName}' exists (region: {location})");
    }
    catch (AggregateException ae) when (ae.InnerException is Amazon.S3.AmazonS3Exception s3Ex)
    {
        // 404 에러 = 버킷 없음
        if (s3Ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"S3 bucket '{s3BucketName}' does not exist", s3Ex);
        }
        
        // 403 에러 = 권한 없음
        if (s3Ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException($"No permission to access S3 bucket '{s3BucketName}'", s3Ex);
        }

        throw new InvalidOperationException($"Failed to connect to S3: {s3Ex.Message}", s3Ex);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Unexpected error: {ex.Message}");
        throw new InvalidOperationException($"Failed to connect to S3: {ex.Message}", ex);
    }

    return s3Client;
});

// Register custom services
builder.Services.AddScoped<IRedisService, RedisService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ICaptchaService, CaptchaService>();

// Cloudflare Turnstile와 통신을 위한 HttpClient 추가
builder.Services.AddHttpClient();

// background 서비스 등록
builder.Services.AddHostedService<UnverifiedAccountCleanupService>();
builder.Services.AddHostedService<TokenCleanupService>();

// HSTS (HTTP Strict Transport Security) 설정
builder.Services.AddHsts(options =>
{
    options.Preload = true; // HSTS Preload 목록에 포함
    options.IncludeSubDomains = true; // 서브도메인에도 적용
    options.MaxAge = TimeSpan.FromDays(365); // 1년간 유지
});

// 프론트엔드 도메인에서의 CORS 허용 설정
builder.Services.AddCors(options =>
{
    var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:3000";
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(frontendUrl)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add controllers
builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// HTTP 요청 파이프라인 구성
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 배포 환경에서만 HTTPS 리디렉션 및 HSTS 사용
if (!app.Environment.IsDevelopment())
{
    // HTTPS 리다이렉트 (HTTP -> HTTPS 자동 전환)
    app.UseHttpsRedirection();

    // HSTS (HTTP Strict Transport Security)
    // 브라우저에게 1년간 HTTPS만 사용하도록 강제
    app.UseHsts();
}

// 프론트엔드 도메인에서의 요청 허용
app.UseCors("AllowFrontend");

// Map controllers
app.MapControllers();

app.MapGet("/health", async (ApplicationDbContext db, IConnectionMultiplexer redis, IAmazonS3 s3) =>
{
    var healthStatus = new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        checks = new Dictionary<string, object>()
    };

    var isHealthy = true;

    try
    {
        // Database check
        var canConnect = await db.Database.CanConnectAsync();
        healthStatus.checks.Add("database", new
        {
            status = canConnect ? "healthy" : "unhealthy",
            message = canConnect ? "Database connection successful" : "Cannot connect to database"
        });

        if (!canConnect)
            isHealthy = false;

        // Redis check
        try
        {
            var redisDb = redis.GetDatabase();
            await redisDb.PingAsync();
            healthStatus.checks.Add("redis", new
            {
                status = "healthy",
                message = "Redis connection successful"
            });
        }
        catch (Exception redisEx)
        {
            healthStatus.checks.Add("redis", new
            {
                status = "unhealthy",
                message = redisEx.Message
            });
            isHealthy = false;
        }

        // S3 check (이미 서버 시작 시 연결 테스트 완료)
        healthStatus.checks.Add("s3", new
        {
            status = "healthy",
            message = $"S3 bucket '{s3BucketName}' connected (verified at startup)"
        });

        if (!isHealthy)
            return Results.Json(
                new { status = "unhealthy", healthStatus.timestamp, healthStatus.checks },
                statusCode: 503
            );

        return Results.Ok(healthStatus);
    }
    catch (Exception ex)
    {
        healthStatus.checks.Add("error", new
        {
            status = "unhealthy",
            message = ex.Message
        });

        return Results.Json(
            new { status = "unhealthy", healthStatus.timestamp, healthStatus.checks },
            statusCode: 503
        );
    }
})
.WithName("HealthCheck")
.WithTags("Health");

app.Run();
