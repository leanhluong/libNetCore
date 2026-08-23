namespace LibNetCore.Core.Primitives;

/// <summary>
/// Một lỗi đặc biệt: nó ôm NHIỀU lỗi con.
///
/// Vì sao cần: <see cref="Result"/> chỉ mang được đúng một <see cref="Error"/>. Nếu để nguyên
/// như vậy thì form 10 ô sai sẽ báo từng ô một — người dùng sửa xong ô này mới lộ ra ô sau,
/// phải gửi lại 10 lần. Thay vì làm <c>Result</c> phức tạp lên để chứa danh sách, ta để một
/// LOẠI LỖI biết cách chứa danh sách. <c>Result</c> giữ nguyên độ đơn giản.
/// </summary>
public sealed record ValidationError : Error
{
    public ValidationError(Error[] errors)
        : base("Validation.Multiple", "Dữ liệu gửi lên có nhiều chỗ không hợp lệ.", ErrorType.Validation)
        => Errors = errors;

    public Error[] Errors { get; }

    /// <summary>
    /// Chạy hết mọi phép kiểm rồi mới gom lỗi — chứ không dừng ở chỗ sai đầu tiên.
    /// </summary>
    public static ValidationError FromResults(IEnumerable<Result> results) =>
        new([.. results.Where(r => r.IsFailure).Select(r => r.Error)]);
}
