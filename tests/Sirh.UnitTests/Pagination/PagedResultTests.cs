using Shouldly;
using Sirh.BuildingBlocks.Application.Pagination;
using Xunit;

namespace Sirh.UnitTests.Pagination;

public sealed class PagedResultTests
{
    [Fact]
    public void PagedRequest_Should_Clamp_Out_Of_Range_Values()
    {
        var low = new PagedRequest { Page = -5, PageSize = 0 };
        low.NormalizedPage.ShouldBe(1);
        low.NormalizedPageSize.ShouldBe(20);
        low.Skip.ShouldBe(0);

        var high = new PagedRequest { Page = 3, PageSize = 500 };
        high.NormalizedPage.ShouldBe(3);
        high.NormalizedPageSize.ShouldBe(100);
        high.Skip.ShouldBe(200);
    }

    [Fact]
    public void PagedResult_Should_Compute_TotalPages()
    {
        var result = PagedResult<string>.Create(["a", "b"], totalCount: 41, page: 1, pageSize: 20);
        result.TotalPages.ShouldBe(3);

        var empty = PagedResult<string>.Create([], totalCount: 0, page: 1, pageSize: 20);
        empty.TotalPages.ShouldBe(0);
    }
}
