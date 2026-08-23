namespace Luong.Kernel.Pagination;

/// <summary>
/// Một trang kết quả kèm đủ thông tin để giao diện vẽ được thanh phân trang.
///
/// Vì sao trả cả <see cref="TotalCount"/> chứ không chỉ trả danh sách: thiếu nó thì
/// frontend không biết có bao nhiêu trang, chỉ đoán được "còn nữa hay hết" sau khi
/// đã gọi thêm một lần. Trả sẵn thì vẽ được ngay "Trang 2/17".
/// </summary>
public sealed class PagedList<T>
{
    private PagedList(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    public IReadOnlyList<T> Items { get; }

    /// <summary>Đếm từ 1, không phải từ 0 — đây là con số hiện ra cho người dùng đọc.</summary>
    public int Page { get; }

    public int PageSize { get; }

    /// <summary>Tổng số bản ghi khớp điều kiện lọc, KHÔNG phải số bản ghi trong trang này.</summary>
    public int TotalCount { get; }

    /// <summary>
    /// Làm tròn LÊN. 10 bản ghi chia 3 mỗi trang là 4 trang — trang cuối chỉ có 1 bản ghi
    /// nhưng vẫn phải tồn tại. Làm tròn xuống là làm mất hẳn nó mà không ai hay.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;

    public static PagedList<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);

        return new PagedList<T>(items, page, pageSize, totalCount);
    }
}
