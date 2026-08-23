namespace LibNetCore.Core.Domain;

/// <summary>
/// Đánh dấu thực thể cần ghi lại "tạo lúc nào, sửa lần cuối lúc nào".
///
/// Vì sao để hạ tầng tự điền thay vì bắt mỗi handler tự gán: chỉ cần MỘT handler quên
/// là có bản ghi không biết sinh ra khi nào — và thường chỉ phát hiện lúc đang đi tìm
/// nguyên nhân một sự cố, tức là đúng lúc cần nó nhất.
///
/// Thuộc tính để <c>set</c> công khai là một nhượng bộ có chủ ý: EF và interceptor cần
/// ghi vào được. Đổi lại, KHÔNG có chỗ nào trong tầng nghiệp vụ được phép gán tay —
/// gán tay nghĩa là đang nói dối về thời điểm.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAtUtc { get; set; }

    DateTimeOffset? UpdatedAtUtc { get; set; }
}
