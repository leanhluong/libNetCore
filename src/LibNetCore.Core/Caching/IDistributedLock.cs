namespace LibNetCore.Core.Caching;

/// <summary>
/// Khoá dùng chung giữa NHIỀU tiến trình.
///
/// <c>lock</c> của C# chỉ chặn được các luồng trong CÙNG một tiến trình. Chạy 3 pod thì
/// mỗi pod có một cái <c>lock</c> riêng, và cả ba cùng vào một lúc — khoá coi như không
/// tồn tại. Khoá dùng chung phải nằm ở một chỗ mà cả ba pod đều nhìn thấy: Redis.
///
/// Ca dùng điển hình: hai webhook cùng lúc gán một hội thoại cho hai người; hai request
/// cùng cấp một số thứ tự; hai job cùng chốt một kỳ.
/// </summary>
public interface IDistributedLock
{
    /// <summary>
    /// Giành khoá. Trả <c>null</c> nếu hết <paramref name="waitFor"/> mà vẫn không giành được.
    /// </summary>
    /// <param name="timeToLive">
    /// Hạn tự nhả. BẮT BUỘC có: pod đang giữ khoá mà chết đột ngột thì không ai nhả hộ,
    /// và khoá vĩnh viễn đó sẽ chặn toàn bộ hệ thống. Đặt dài hơn thời gian việc thật cần,
    /// nhưng đừng dài quá — càng dài thì lúc pod chết càng lâu mới hồi phục.
    /// </param>
    Task<ILockHandle?> TryAcquireAsync(
        string key,
        TimeSpan timeToLive,
        TimeSpan waitFor,
        CancellationToken cancellationToken = default);
}

/// <summary>Khoá đang giữ. Nhả bằng cách <c>await using</c>.</summary>
public interface ILockHandle : IAsyncDisposable
{
    string Key { get; }

    /// <summary>Mã riêng của lần giành này — thứ bảo đảm không nhả nhầm khoá của người khác.</summary>
    string Token { get; }
}

/// <summary>Cổng hẹp xuống kho khoá. Bản Redis nằm ở <c>LibNetCore.Caching</c>.</summary>
public interface ILockStore
{
    Task<bool> TryAcquireAsync(string key, string token, TimeSpan timeToLive, CancellationToken cancellationToken = default);

    Task<bool> ReleaseAsync(string key, string token, CancellationToken cancellationToken = default);
}
