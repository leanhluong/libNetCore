using LibNetCore.Core.Text;

namespace LibNetCore.EntityFrameworkCore.Conventions;

/// <summary>
/// Đổi tên kiểu C# (<c>FullName</c>) sang tên kiểu PostgreSQL (<c>full_name</c>).
///
/// Vì sao PostgreSQL cần snake_case: định danh không đặt trong dấu nháy kép sẽ bị
/// PostgreSQL tự hạ hết thành chữ thường. Bảng tên <c>Employees</c> vì thế phải viết
/// <c>SELECT * FROM "Employees"</c> — quên cặp nháy là lỗi "relation employees does not
/// exist". Ai mở psql lên gõ tay đều dính. Đặt tên snake_case ngay từ đầu thì không
/// bao giờ phải nhớ tới cặp nháy đó nữa.
///
/// Phép cắt từ dùng chung với khoá định tuyến RabbitMQ, nên nó nằm ở
/// <see cref="CaseConverter"/> trong <c>LibNetCore.Core</c>.
/// </summary>
public static class SnakeCaseNameConverter
{
    public static string ToSnakeCase(string name) => CaseConverter.ToSnakeCase(name);
}
