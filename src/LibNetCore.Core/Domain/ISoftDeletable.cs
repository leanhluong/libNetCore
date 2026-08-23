namespace LibNetCore.Core.Domain;

/// <summary>
/// Đánh dấu thực thể KHÔNG được xoá hẳn khỏi bảng, chỉ được đánh dấu là đã xoá.
///
/// Vì sao: hồ sơ nhân viên, đơn từ, chứng từ đều phải tra cứu lại được sau khi "xoá" —
/// để đối soát, để trả lời khiếu nại, để biết ai từng làm ở đây. Xoá hẳn còn làm gãy
/// mọi bản ghi khác đang trỏ tới nó.
///
/// Cái giá phải trả: mọi truy vấn từ nay phải nhớ loại bỏ hàng đã xoá. Bắt người viết
/// query tự nhớ là hỏng — chỉ cần quên MỘT lần là dữ liệu đã xoá lộ ra ngoài. Nên việc
/// đó giao cho bộ lọc toàn cục ở tầng hạ tầng, không cho ai phải nhớ.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTimeOffset? DeletedAtUtc { get; set; }
}
