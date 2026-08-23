using System.Text;

namespace LibNetCore.Core.Text;

/// <summary>
/// Đổi tên viết kiểu C# (<c>FullName</c>) sang các kiểu viết mà thế giới bên ngoài dùng.
///
/// Đặt ở <c>Core</c> vì có hai nơi cần đúng một phép cắt từ này với hai dấu ngăn khác nhau:
/// EF cần <c>full_name</c> cho PostgreSQL, còn RabbitMQ cần <c>employee-hired</c> cho khoá
/// định tuyến. Viết hai lần thì sớm muộn hai bản sẽ lệch nhau ở đúng những ca khó
/// (viết tắt liền nhau, tên đã có sẵn dấu ngăn).
/// </summary>
public static class CaseConverter
{
    public static string ToSnakeCase(string name) => Convert(name, '_');

    public static string ToKebabCase(string name) => Convert(name, '-');

    private static string Convert(string name, char separator)
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
                    builder.Append(separator);
                }

                builder.Append(char.ToLowerInvariant(current));
            }
            else
            {
                builder.Append(current == '_' || current == '-' ? separator : current);
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

        // Đã có dấu ngăn sẵn thì đừng thêm nữa: "PK_Employees" -> "pk_employees".
        if (previous is '_' or '-')
        {
            return false;
        }

        // Ranh giới thường -> HOA: "fullName" -> "full_name".
        if (char.IsLower(previous) || char.IsDigit(previous))
        {
            return true;
        }

        // Cụm viết tắt rồi tới từ mới: "OTPCode" -> "otp_code", chứ không "o_t_p_code".
        return index + 1 < name.Length && char.IsLower(name[index + 1]);
    }
}
