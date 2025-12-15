using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using api.Data;
using api.Services;

// Npgsql DateTime 설정 - UTC를 timestamp without time zone에 쓸 수 있도록
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

Env.Load();

var dbHost = Environment.GetEnvironmentVariable("DATABASE_HOST") ?? "localhost";
var dbPort = Environment.GetEnvironmentVariable("DATABASE_PORT") ?? "5432";
var dbName = Environment.GetEnvironmentVariable("DATABASE_NAME") ?? "riff";
var dbUser = Environment.GetEnvironmentVariable("DATABASE_USER") ?? "postgres";
var dbPassword = Environment.GetEnvironmentVariable("DATABASE_PASSWORD") ?? "";

var connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword}";

// JWT 환경 변수 검증
var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
var jwtExpirationMinutes = Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES");

if (string.IsNullOrEmpty(jwtSecretKey))
{
    throw new InvalidOperationException("JWT_SECRET_KEY environment variable is required");
}

if (string.IsNullOrEmpty(jwtIssuer))
{
    throw new InvalidOperationException("JWT_ISSUER environment variable is required");
}

if (string.IsNullOrEmpty(jwtExpirationMinutes) || !int.TryParse(jwtExpirationMinutes, out _))
{
    throw new InvalidOperationException("JWT_EXPIRATION_MINUTES environment variable is required and must be a valid integer");
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register custom services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITokenService, TokenService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000") // Next.js dev server
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Use CORS - must be before UseAuthorization
app.UseCors("AllowFrontend");

// Only use HTTPS redirection in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Map controllers
app.MapControllers();

app.MapGet("/health", async (ApplicationDbContext db) =>
{
    var healthStatus = new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        checks = new Dictionary<string, object>()
    };

    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        healthStatus.checks.Add("database", new
        {
            status = canConnect ? "healthy" : "unhealthy",
            message = canConnect ? "Database connection successful" : "Cannot connect to database"
        });

        if (!canConnect)
        {
            return Results.Json(
                new { status = "unhealthy", healthStatus.timestamp, healthStatus.checks },
                statusCode: 503
            );
        }

        return Results.Ok(healthStatus);
    }
    catch (Exception ex)
    {
        healthStatus.checks.Add("database", new
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
