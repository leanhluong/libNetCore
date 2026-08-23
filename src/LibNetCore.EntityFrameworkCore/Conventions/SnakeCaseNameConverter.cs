using System.Text;

namespace LibNetCore.EntityFrameworkCore.Conventions;

/// <summary>
/// Đổi tên kiểu C# (<c>FullName</c>) sang tên kiểu PostgreSQL (<c>full_name</c>).
///
/// Vì sao PostgreSQL cần snake_case: định danh không đặt trong dấu nháy kép sẽ bị
/// PostgreSQL tự hạ hết thành chữ thường. Bảng tên <c>Employees</c> vì thế phải viết
/// <c>SELECT * FROM "Employees"</c> — quên cặp nháy là lỗi "relation employees does not
/// exist". Ai mở psql lên gõ tay đều dính. Đặt tên snake_case ngay từ đầu thì không
/// bao giờ phải nhớ tới cặp nháy đó nữa.
/// </summary>
public static class SnakeCaseNameConverter
{
    public static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var builder = new StringBuilder(name.Length + 8);

        for (int i = 0; i < name.Length; i++)
        {
            char current = name[i];

            if (char.IsUpper(current))
            {
                if (ShouldInsertSeparator(name, i))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }

    private static bool ShouldInsertSeparator(string name, int index)
    {
        if (index == 0)
        {
            return false;
        }

        char previous = name[index - 1];

        // Đã có dấu ngăn sẵn rồi thì đừng thêm nữa: "PK_Employees" -> "pk_employees",
        // không phải "pk__employees".
        if (previous == '_')
        {
            return false;
        }

        // Ranh giới thường -> HOA: "fullName" -> "full_name".
        if (char.IsLower(previous) || char.IsDigit(previous))
        {
            return true;
        }

        // Cụm viết tắt rồi tới từ mới: "OTPCode" -> "otp_code".
        // Cắt trước chữ hoa CUỐI của cụm, chứ không cắt từng chữ một ("o_t_p_code").
        return index + 1 < name.Length && char.IsLower(name[index + 1]);
    }
}
