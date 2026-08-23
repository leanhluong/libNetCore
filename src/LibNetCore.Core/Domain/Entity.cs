namespace LibNetCore.Core.Domain;

/// <summary>
/// Thực thể: thứ được nhận diện bằng DANH TÍNH, không bằng nội dung.
///
/// Một nhân viên đổi tên, đổi phòng ban, đổi số điện thoại — vẫn là đúng người đó,
/// vì <see cref="Id"/> không đổi. Ngược lại, hai nhân viên trùng tên trùng ngày sinh
/// vẫn là hai người khác nhau.
///
/// Vì sao phải tự viết so sánh: mặc định C# so sánh hai đối tượng bằng Ô NHỚ. Cùng một
/// nhân viên đọc từ database ở hai chỗ khác nhau sẽ nằm ở hai ô nhớ → mặc định bảo
/// "khác nhau", trong khi thực tế là một người. Sai từ đây kéo theo sai cả
/// <c>Contains</c>, <c>Distinct</c>, <c>Dictionary</c>.
///
/// Đối lập với thực thể là "đối tượng giá trị" (tiền, khoảng thời gian, địa chỉ) —
/// thứ so sánh bằng NỘI DUNG. Ta KHÔNG viết lớp gốc cho nó: <c>record</c> của C#
/// đã làm sẵn đúng như vậy rồi.
/// </summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    protected Entity(TId id) => Id = id;

    /// <summary>Dành cho EF Core dựng lại đối tượng khi đọc từ database.</summary>
    protected Entity() => Id = default!;

    public TId Id { get; protected init; }

    public bool Equals(Entity<TId>? other) =>
        other is not null
        && other.GetType() == GetType()   // nhân viên và phòng ban lỡ trùng Id vẫn là hai thứ khác nhau
        && other.Id.Equals(Id);

    public override bool Equals(object? obj) => obj is Entity<TId> entity && Equals(entity);

    /// <summary>
    /// Phải khớp với <see cref="Equals(Entity{TId}?)"/>. Thiếu chỗ này thì hai thực thể
    /// "bằng nhau" vẫn rơi vào hai ngăn khác nhau của <c>Dictionary</c>/<c>HashSet</c> —
    /// một loại lỗi rất khó lần ra vì code trông hoàn toàn hợp lý.
    /// </summary>
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !Equals(left, right);
}
