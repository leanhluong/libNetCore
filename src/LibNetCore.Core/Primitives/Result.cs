namespace LibNetCore.Core.Primitives;

/// <summary>
/// Kết quả của một thao tác: hoặc thành công, hoặc thất bại kèm <see cref="Primitives.Error"/>.
///
/// Vì sao không dùng exception cho mọi thất bại: exception là để nói "chuyện KHÔNG lường trước
/// vừa xảy ra". Còn "sai mật khẩu", "email đã tồn tại", "không tìm thấy phòng ban" đều là
/// chuyện ĐÃ lường trước — chúng là một nhánh nghiệp vụ bình thường, không phải sự cố.
/// Ném exception cho chúng vừa đắt (dựng stack trace), vừa giấu mất luồng thật của code:
/// đọc chữ ký hàm không biết được nó có thể hỏng kiểu gì.
///
/// Exception vẫn dùng — nhưng để dành cho thứ thật sự bất thường: mất kết nối DB, hết bộ nhớ,
/// và những trạng thái vô lý do lập trình sai (xem các chỗ ném ở dưới).
/// </summary>
public class Result
{
    protected internal Result(bool isSuccess, Error error)
    {
        // Hai trạng thái vô lý, chặn ngay tại chỗ tạo ra chúng.
        if (isSuccess && error != Error.None)
        {
            throw new ArgumentException("Kết quả thành công thì không được mang lỗi.", nameof(error));
        }

        if (!isSuccess && error == Error.None)
        {
            throw new ArgumentException("Kết quả thất bại thì phải nói được vì sao.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary><see cref="Primitives.Error.None"/> khi thành công.</summary>
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<TValue>(value, true, Error.None);
    }

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

/// <summary>Kết quả có mang theo giá trị khi thành công.</summary>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
        => _value = value;

    /// <summary>
    /// Giá trị khi thành công. Đọc lúc thất bại là hỏi sai câu hỏi nên sẽ ném lỗi —
    /// trả về <c>null</c> ở đây chỉ khiến lỗi trôi đi rồi nổ ở một chỗ khác xa hơn,
    /// lúc đó không còn biết nguyên nhân gốc ở đâu.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Không đọc được giá trị của một kết quả thất bại.");

    /// <summary>Cho phép handler viết thẳng <c>return user;</c>.</summary>
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    /// <summary>Cho phép handler viết thẳng <c>return Error.NotFound(...);</c>.</summary>
    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);
}
