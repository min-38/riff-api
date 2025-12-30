using api.Data;
using Microsoft.EntityFrameworkCore;

namespace api.BackgroundServices;

// 만료된 토큰을 주기적으로 정리하는 백그라운드 서비스
public class TokenCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TokenCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1); // 1시간마다 실행

    public TokenCleanupService(
        IServiceProvider serviceProvider,
        ILogger<TokenCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Token Cleanup Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredTokensAsync();
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while cleaning up expired tokens");
                // 에러가 발생해도 서비스는 계속 실행
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // 에러 시 5분 후 재시도
            }
        }

        _logger.LogInformation("Token Cleanup Service stopped");
    }

    private async Task CleanupExpiredTokensAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;

        // 1. 만료된 이메일 인증 토큰 정리
        var expiredEmailVerificationUsers = await context.Users
            .Where(u => u.EmailVerificationToken != null &&
                       u.EmailVerificationTokenExpiredAt != null &&
                       u.EmailVerificationTokenExpiredAt < now)
            .ToListAsync();

        if (expiredEmailVerificationUsers.Any())
        {
            foreach (var user in expiredEmailVerificationUsers)
            {
                user.EmailVerificationToken = null;
                user.EmailVerificationTokenExpiredAt = null;
            }

            await context.SaveChangesAsync();
            _logger.LogInformation("Cleaned up {Count} expired email verification tokens",
                expiredEmailVerificationUsers.Count);
        }

        // 2. 만료된 비밀번호 재설정 토큰 정리
        var expiredPasswordResetUsers = await context.Users
            .Where(u => u.PasswordResetToken != null &&
                       u.PasswordResetTokenExpiredAt != null &&
                       u.PasswordResetTokenExpiredAt < now)
            .ToListAsync();

        if (expiredPasswordResetUsers.Any())
        {
            foreach (var user in expiredPasswordResetUsers)
            {
                user.PasswordResetToken = null;
                user.PasswordResetTokenExpiredAt = null;
            }

            await context.SaveChangesAsync();
            _logger.LogInformation("Cleaned up {Count} expired password reset tokens",
                expiredPasswordResetUsers.Count);
        }

        // 3. 만료된 리프레시 토큰 정리
        var expiredRefreshTokens = await context.RefreshTokens
            .Where(rt => rt.ExpiresAt < now)
            .ToListAsync();

        if (expiredRefreshTokens.Any())
        {
            context.RefreshTokens.RemoveRange(expiredRefreshTokens);
            await context.SaveChangesAsync();
            _logger.LogInformation("Cleaned up {Count} expired refresh tokens",
                expiredRefreshTokens.Count);
        }

        if (!expiredEmailVerificationUsers.Any() &&
            !expiredPasswordResetUsers.Any() &&
            !expiredRefreshTokens.Any())
        {
            _logger.LogDebug("No expired tokens to clean up");
        }
    }
}
