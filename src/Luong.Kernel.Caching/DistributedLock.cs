using Luong.Kernel.Caching;
using Microsoft.Extensions.Logging;

namespace Luong.Kernel.Caching;

/// <summary>
/// Phần giao thức của khoá dùng chung: giành, thử lại, bỏ cuộc, nhả.
///
/// Tách khỏi Redis để test được toàn bộ luật mà không cần dựng Redis — kho khoá thật
/// nằm sau <see cref="ILockStore"/>, và bản Redis của nó chỉ là mấy dòng chuyển tiếp.
/// </summary>
public sealed class DistributedLock(
    ILockStore store,
    ILogger<DistributedLock> logger,
    TimeSpan? retryDelay = null) : IDistributedLock
{
    private readonly TimeSpan _retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(50);

    public async Task<ILockHandle?> TryAcquireAsync(
        string key,
        TimeSpan timeToLive,
        TimeSpan waitFor,
        CancellationToken cancellationToken = default)
    {
        // Mã riêng cho MỖI lần giành. Đây là thứ ngăn kịch bản nhả nhầm khoá của
        // người khác sau khi khoá của mình đã hết hạn — xem ghi chú ở ReleaseAsync.
        string token = Guid.NewGuid().ToString("N");

        var deadline = DateTimeOffset.UtcNow + waitFor;

        while (true)
        {
            if (await store.TryAcquireAsync(key, token, timeToLive, cancellationToken))
            {
                return new LockHandle(store, key, token, logger);
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                // Bỏ cuộc chứ không chờ mãi. Request đang chờ khoá vẫn giữ một luồng và
                // một kết nối database — vài trăm request cùng chờ là cả service đứng,
                // không riêng phần dùng khoá.
                logger.LogDebug("Không giành được khoá {Key} trong {WaitFor}.", key, waitFor);
                return null;
            }

            await Task.Delay(_retryDelay, cancellationToken);
        }
    }

    private sealed class LockHandle(ILockStore store, string key, string token, ILogger logger) : ILockHandle
    {
        private bool _released;

        public string Key => key;

        public string Token => token;

        public async ValueTask DisposeAsync()
        {
            if (_released)
            {
                return;
            }

            _released = true;

            try
            {
                // Nhả kèm mã. Kho khoá chỉ xoá nếu ĐÚNG mã này đang giữ. Thiếu bước đó thì:
                //   A giành khoá 30 giây → A chạy quá 30 giây → khoá tự hết hạn
                //   → B giành được → A xong việc, nhả khoá → A vừa nhả MẤT khoá CỦA B
                //   → C giành được → B và C cùng chạy. Khoá coi như không tồn tại.
                if (!await store.ReleaseAsync(key, token, CancellationToken.None))
                {
                    logger.LogWarning(
                        "Khoá {Key} đã không còn thuộc về lần giành này khi nhả — nhiều khả năng công việc chạy lâu hơn hạn khoá.",
                        key);
                }
            }
            catch (Exception exception)
            {
                // Nhả hỏng không được làm hỏng luồng nghiệp vụ: khoá vẫn tự hết hạn.
                logger.LogError(exception, "Nhả khoá {Key} thất bại.", key);
            }
        }
    }
}
