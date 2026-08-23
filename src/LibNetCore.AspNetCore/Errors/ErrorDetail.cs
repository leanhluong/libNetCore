namespace LibNetCore.AspNetCore.Errors;

/// <summary>
/// Một dòng lỗi trong phần thân phản hồi.
///
/// Tách riêng khỏi <see cref="LibNetCore.Core.Primitives.Error"/> vì hai thứ này phục vụ
/// hai người khác nhau: <c>Error</c> là ngôn ngữ nội bộ của code (có cả <c>Type</c> để
/// quyết định mã HTTP), còn <c>ErrorDetail</c> là thứ ĐI RA NGOÀI cho frontend đọc.
/// Không đẩy <c>Type</c> ra ngoài vì mã HTTP đã nói điều đó rồi.
/// </summary>
public sealed record ErrorDetail(string Code, string Description);
