using System.Text.Json;
using StackExchange.Redis;

namespace api.Services;

public class RedisService : IRedisService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly ILogger<RedisService> _logger;

    public RedisService(IConnectionMultiplexer redis, ILogger<RedisService> logger)
    {
        _redis = redis;
        _db = redis.GetDatabase();
        _logger = logger;
    }

    // 데이터 저장
    public async Task SetAsync<T>(string key, T data, TimeSpan? expiry = null)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);

            // 24시간 기본 만료 시간
            var expiryTime = expiry ?? TimeSpan.FromHours(24);
            await _db.StringSetAsync(key, json, expiryTime);

            _logger.LogInformation(
                "Redis: Saved data for key '{Key}' (Type: {Type}, TTL: {TTL}s)",
                key, typeof(T).Name, (int)expiryTime.TotalSeconds
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis: Failed to save data for key '{Key}'", key);
            throw;
        }
    }

    // 데이터 가져오기
    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var json = await _db.StringGetAsync(key);

            if (json.IsNullOrEmpty)
            {
                _logger.LogInformation("Redis: No data found for key '{Key}'", key);
                return null;
            }

            var data = JsonSerializer.Deserialize<T>(json.ToString());
            _logger.LogInformation(
                "Redis: Retrieved data for key '{Key}' (Type: {Type})",
                key, typeof(T).Name
            );

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis: Failed to get data for key '{Key}'", key);
            throw;
        }
    }

    // 데이터 삭제
    public async Task<bool> DeleteAsync(string key)
    {
        try
        {
            var result = await _db.KeyDeleteAsync(key);

            if (result)
                _logger.LogInformation("Redis: Deleted data for key '{Key}'", key);
            else
                _logger.LogWarning("Redis: No data to delete for key '{Key}' (key not found)", key);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis: Failed to delete data for key '{Key}'", key);
            throw;
        }
    }

    // 키 존재 여부 확인
    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            var exists = await _db.KeyExistsAsync(key);

            _logger.LogInformation("Redis: Key existence check for '{Key}': {Exists}", key, exists);

            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis: Failed to check if key '{Key}' exists", key);
            throw;
        }
    }

    // 카운터 증가 (Rate Limiting용)
    public async Task<long> IncrementAsync(string key, TimeSpan? expiry = null)
    {
        try
        {
            var value = await _db.StringIncrementAsync(key);

            // 첫 번째 증가일 때만 만료 시간 설정
            if (value == 1 && expiry.HasValue)
                await _db.KeyExpireAsync(key, expiry.Value);

            _logger.LogInformation(
                "Redis: Incremented counter for key '{Key}' to {Value}",
                key, value
            );

            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis: Failed to increment counter for key '{Key}'", key);
            throw;
        }
    }
}
