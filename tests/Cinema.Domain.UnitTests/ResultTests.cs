using Cinema.Domain.Common.Models;
using FluentAssertions;
using Xunit;

namespace Cinema.Domain.UnitTests.Common;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeEmpty();
    }

    [Fact]
    public void Failure_ShouldCreateFailedResult()
    {
        var result = Result.Failure("Something went wrong");

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Something went wrong");
    }

    [Fact]
    public void SuccessWithValue_ShouldCreateSuccessfulResultWithValue()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void FailureWithValue_ShouldCreateFailedResultWithDefaultValue()
    {
        var result = Result.Failure<int>("Error");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Error");
    }

    [Fact]
    public void SuccessWithComplexType_ShouldPreserveValue()
    {
        var person = new TestPerson("John", 30);

        var result = Result.Success(person);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("John");
        result.Value.Age.Should().Be(30);
    }

    private record TestPerson(string Name, int Age);
}
