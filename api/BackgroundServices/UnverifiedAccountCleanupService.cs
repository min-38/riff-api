using Microsoft.EntityFrameworkCore;
using api.Data;

namespace api.BackgroundServices;

// BackgroundService는 .NET에서 제공하는 백그라운드 작업을 위한 추상 클래스
// 앱이 실행되는 동안 계속 백그라운드에서 실행됨
public class UnverifiedAccountCleanupService : BackgroundService
{
    private readonly ILogger<UnverifiedAccountCleanupService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1); // 1시간마다 실행

    public UnverifiedAccountCleanupService(
        ILogger<UnverifiedAccountCleanupService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    // BackgroundService의 핵심 메서드
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Unverified Account Cleanup Service started");

        while (!stoppingToken.IsCancellationRequested) // 서비스가 중지되지 않은 동안
        {
            try
            {
                await CleanupExpiredAccountsAsync(); // 만료된 미인증 계정 정리
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during account cleanup");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        _logger.LogInformation("Unverified Account Cleanup Service stopped");
    }

    private async Task CleanupExpiredAccountsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); // DbContext 가져옴

        // 24시간 지난 미인증 계정 조회
        var expiredAccounts = await context.Users
            .Where(u => u.Verified == false &&
                        u.EmailVerificationTokenExpiredAt.HasValue &&
                        u.EmailVerificationTokenExpiredAt.Value < DateTime.UtcNow)
            .ToListAsync();

        if (expiredAccounts.Any()) // 만료된 계정이 있으면
        {
            _logger.LogInformation("Found {Count} expired unverified accounts to delete", expiredAccounts.Count);

            // 실제 삭제하지 않고 시간만 등록
            // 나중에 실제 삭제로 바꿀 수도 있음
            foreach (var account in expiredAccounts)
                account.DeletedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();

            _logger.LogInformation("Deleted {Count} expired unverified accounts", expiredAccounts.Count);
        }
    }
}
