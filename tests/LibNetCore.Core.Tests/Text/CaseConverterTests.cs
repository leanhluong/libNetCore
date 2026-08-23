using LibNetCore.Core.Text;

namespace LibNetCore.Core.Tests.Text;

public class CaseConverterTests
{
    [Theory]
    [InlineData("FullName", "full_name")]
    [InlineData("OTPCode", "otp_code")]
    [InlineData("PK_Employees", "pk_employees")]
    [InlineData("Id", "id")]
    [InlineData("", "")]
    public void ToSnakeCase_UsesUnderscores(string input, string expected)
    {
        Assert.Equal(expected, CaseConverter.ToSnakeCase(input));
    }

    // Khoá định tuyến của RabbitMQ theo thông lệ dùng gạch ngang, không gạch dưới.
    [Theory]
    [InlineData("EmployeeHired", "employee-hired")]
    [InlineData("OTPRequested", "otp-requested")]
    [InlineData("LeaveRequestApproved", "leave-request-approved")]
    [InlineData("", "")]
    public void ToKebabCase_UsesHyphens(string input, string expected)
    {
        Assert.Equal(expected, CaseConverter.ToKebabCase(input));
    }
}
