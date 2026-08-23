using LibNetCore.Caching;

namespace LibNetCore.Caching.Tests;

public class CacheKeyTests
{
    [Fact]
    public void Build_JoinsSegmentsWithColons()
    {
        Assert.Equal("onooffice:employee:42", CacheKey.Build("onooffice", "employee", "42"));
    }

    [Fact]
    public void Build_LowercasesEverything()
    {
        Assert.Equal("onooffice:employee:abc", CacheKey.Build("ONoOffice", "Employee", "ABC"));
    }

    // Khoảng trắng trong khoá làm lệnh redis-cli phải bọc nháy - lúc đi dò lỗi rất phiền.
    [Fact]
    public void Build_ReplacesSpaces()
    {
        Assert.Equal("hr:phong-ban:ke-toan", CacheKey.Build("hr", "phong ban", "ke toan"));
    }

    [Fact]
    public void Build_RejectsEmptySegments()
    {
        Assert.Throws<ArgumentException>(() => CacheKey.Build("hr", "", "42"));
    }

    [Fact]
    public void Build_RejectsNoSegments()
    {
        Assert.Throws<ArgumentException>(() => CacheKey.Build());
    }
}
