using Shouldly;
using Sirh.BuildingBlocks.Domain.Results;
using Xunit;

namespace Sirh.UnitTests.Results;

public sealed class ResultTests
{
    [Fact]
    public void Success_Result_Should_Expose_Value()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void Failure_Result_Should_Throw_When_Accessing_Value()
    {
        Result<int> result = Error.NotFound("Test.Missing", "Missing item");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        Should.Throw<InvalidOperationException>(() => _ = result.Value);
    }
}
