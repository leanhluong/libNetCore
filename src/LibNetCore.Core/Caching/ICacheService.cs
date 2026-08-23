namespace LibNetCore.Core.Caching;

/// <summary>
/// Cổng đọc/ghi bộ nhớ đệm.
///
/// Nằm ở <c>Core</c> nên tầng Application gọi được mà không cần biết phía dưới là Redis,
/// là bộ nhớ trong, hay là gì khác. Đây cũng là thứ khiến use case test được mà không
/// phải dựng Redis.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Có trong đệm thì lấy ra; không có thì gọi <paramref name="factory"/>, cất lại rồi trả về.
    ///
    /// Đây là dạng dùng nhiều nhất, và cũng là dạng dễ viết sai nhất nếu tự làm mỗi chỗ
    /// một kiểu — nhất là chuyện <b>giá trị rỗng cũng phải được cất</b>: nếu coi "rỗng"
    /// nghĩa là "chưa có trong đệm" thì mỗi lần hỏi một thứ KHÔNG tồn tại đều đâm thẳng
    /// xuống database. Ai đó gọi liên tục một mã nhân viên không có thật là đủ làm nghẽn
    /// database, dù bộ đệm vẫn "hoạt động bình thường".
    /// </summary>
    Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? timeToLive = null,
        CancellationToken cancellationToken = default);
}
