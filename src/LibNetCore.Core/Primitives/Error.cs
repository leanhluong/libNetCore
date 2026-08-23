namespace LibNetCore.Core.Primitives;

/// <summary>
/// Một thất bại đã được lường trước.
///
/// Vì sao có <see cref="Code"/> chứ không chỉ có câu chữ: frontend cần rẽ nhánh
/// theo một thứ ỔN ĐỊNH. Câu chữ thì đổi, dịch sang tiếng khác, sửa chính tả —
/// mã thì không. Quy ước đặt mã: "{Vùng}.{ChuyệnGìXảyRa}", ví dụ "Auth.InvalidCredentials".
///
/// Là <c>record</c> nên hai lỗi cùng nội dung tự khắc bằng nhau — cần tính chất này
/// để service dùng lib so sánh lỗi trong test.
/// </summary>
public record Error(string Code, string Description, ErrorType Type)
{
    /// <summary>Chỗ trống đại diện cho "không có lỗi". Chỉ <see cref="Result"/> thành công mới mang giá trị này.</summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);

    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error Unauthorized(string code, string description) => new(code, description, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string description) => new(code, description, ErrorType.Forbidden);
}
