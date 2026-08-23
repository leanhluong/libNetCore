using Luong.Kernel.Pagination;

namespace Luong.Kernel.Tests.Pagination;

public class PagedListTests
{
    // 10 bản ghi, mỗi trang 3 -> 4 trang (trang cuối chỉ có 1). Làm tròn LÊN.
    // Làm tròn xuống là mất hẳn bản ghi cuối mà không ai biết.
    [Fact]
    public void TotalPages_RoundsUp()
    {
        var page = PagedList<string>.Create(["a", "b", "c"], page: 1, pageSize: 3, totalCount: 10);

        Assert.Equal(4, page.TotalPages);
    }

    [Fact]
    public void EmptyResult_HasNoPages()
    {
        var page = PagedList<string>.Create([], page: 1, pageSize: 20, totalCount: 0);

        Assert.Equal(0, page.TotalPages);
        Assert.False(page.HasNextPage);
        Assert.False(page.HasPreviousPage);
    }

    [Fact]
    public void FirstPage_HasNoPreviousButHasNext()
    {
        var page = PagedList<string>.Create(["a"], page: 1, pageSize: 1, totalCount: 3);

        Assert.False(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public void LastPage_HasPreviousButNoNext()
    {
        var page = PagedList<string>.Create(["c"], page: 3, pageSize: 1, totalCount: 3);

        Assert.True(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -5)]
    public void InvalidPaging_Throws(int page, int pageSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PagedList<string>.Create([], page, pageSize, totalCount: 0));
    }

    [Fact]
    public void NegativeTotalCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PagedList<string>.Create([], page: 1, pageSize: 10, totalCount: -1));
    }
}
