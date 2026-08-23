using LibNetCore.Core.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LibNetCore.Caching.Tests;

internal sealed record Employee(Guid Id, string FullName);

public class CacheServiceTests
{
    private readonly ICacheService _cache;

    public CacheServiceTests()
    {
        // MemoryDistributedCache cài đúng IDistributedCache như Redis - nên test ở đây
        // kiểm được TOÀN BỘ luật của CacheService mà không cần dựng Redis.
        // Thứ nó KHÔNG kiểm được: hành vi qua mạng và chia sẻ giữa nhiều pod.
        var backing = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        _cache = new CacheService(backing, NullLogger<CacheService>.Instance);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullWhenNothingCached()
    {
        Assert.Null(await _cache.GetAsync<Employee>("hr:employee:1"));
    }

    [Fact]
    public async Task SetThenGet_ReturnsTheSameValue()
    {
        var employee = new Employee(Guid.NewGuid(), "Lê Anh Lượng");

        await _cache.SetAsync("hr:employee:1", employee, TimeSpan.FromMinutes(5));

        Assert.Equal(employee, await _cache.GetAsync<Employee>("hr:employee:1"));
    }

    [Fact]
    public async Task RemoveAsync_ClearsTheEntry()
    {
        await _cache.SetAsync("hr:employee:1", new Employee(Guid.NewGuid(), "A"), TimeSpan.FromMinutes(5));

        await _cache.RemoveAsync("hr:employee:1");

        Assert.Null(await _cache.GetAsync<Employee>("hr:employee:1"));
    }

    [Fact]
    public async Task GetOrSet_OnMiss_CallsTheFactoryAndCachesTheResult()
    {
        int calls = 0;

        var first = await _cache.GetOrSetAsync(
            "hr:employee:1",
            _ => { calls++; return Task.FromResult(new Employee(Guid.Empty, "Lê Anh Lượng")); },
            TimeSpan.FromMinutes(5));

        Assert.Equal("Lê Anh Lượng", first.FullName);
        Assert.Equal(1, calls);
    }

    // Đây là toàn bộ lý do GetOrSet tồn tại: lần thứ hai KHÔNG được đụng tới database.
    [Fact]
    public async Task GetOrSet_OnHit_DoesNotCallTheFactoryAgain()
    {
        int calls = 0;
        Task<Employee> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult(new Employee(Guid.Empty, "Lê Anh Lượng"));
        }

        await _cache.GetOrSetAsync("hr:employee:1", Factory, TimeSpan.FromMinutes(5));
        var second = await _cache.GetOrSetAsync("hr:employee:1", Factory, TimeSpan.FromMinutes(5));

        Assert.Equal(1, calls);
        Assert.Equal("Lê Anh Lượng", second.FullName);
    }

    // Cache giá trị null là bẫy: nếu coi "null" nghĩa là "chưa có trong cache" thì
    // mỗi lần hỏi một nhân viên KHÔNG tồn tại đều đâm xuống database - đúng kiểu
    // tấn công làm sập cache (cache penetration).
    [Fact]
    public async Task GetOrSet_CachesTheAbsenceOfAValue()
    {
        int calls = 0;
        Task<Employee?> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult<Employee?>(null);
        }

        await _cache.GetOrSetAsync("hr:employee:missing", Factory, TimeSpan.FromMinutes(5));
        await _cache.GetOrSetAsync("hr:employee:missing", Factory, TimeSpan.FromMinutes(5));

        Assert.Equal(1, calls);
    }
}
