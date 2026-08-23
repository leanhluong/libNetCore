namespace Luong.Kernel.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Ném khi có ai đó cố ghi dữ liệu sang workspace KHÁC workspace đang đăng nhập.
///
/// <b>Vì sao ném exception chứ không trả <c>Result</c>:</b> mọi thất bại nghiệp vụ đều
/// trả <c>Result</c>, nhưng cái này KHÔNG phải thất bại nghiệp vụ — nó là một lỗi lập
/// trình hoặc một cuộc tấn công. Không có luồng xử lý hợp lệ nào cho nó cả, và cách
/// đúng duy nhất là dừng ngay giao dịch.
///
/// <b>Vì sao không lặng lẽ sửa lại giá trị cho đúng:</b> sửa lặng lẽ nghĩa là một lỗi
/// lập trình biến thành dữ liệu ghi nhầm chỗ, và không ai biết cho tới khi khách hàng
/// phát hiện — lúc đó không còn cách nào lần ra hàng nào đã bị ghi sai.
/// </summary>
public sealed class CrossTenantWriteException(string message) : InvalidOperationException(message);
