namespace api.Services;

public interface IRedisService
{
    Task SetAsync<T>(string key, T data, TimeSpan? expiry = null);
    Task<T?> GetAsync<T>(string key) where T : class;
    Task<bool> DeleteAsync(string key);
    Task<bool> ExistsAsync(string key);
    Task<long> IncrementAsync(string key, TimeSpan? expiry = null);
}
