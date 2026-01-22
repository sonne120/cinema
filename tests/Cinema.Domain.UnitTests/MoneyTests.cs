using Cinema.Domain.Common.Models;
using FluentAssertions;
using Xunit;

namespace Cinema.Domain.UnitTests.Common;

public class MoneyTests
{
    [Fact]
    public void Create_WithValidAmount_ShouldCreateMoney()
    {
        var money = Money.Create(100.50m, "USD");

        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldThrowException()
    {
        var act = () => Money.Create(-10m);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*negative*");
    }

    [Fact]
    public void Zero_ShouldCreateZeroMoney()
    {
        var money = Money.Zero();

        money.Amount.Should().Be(0);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Zero_WithCurrency_ShouldCreateZeroMoneyWithCurrency()
    {
        var money = Money.Zero("EUR");

        money.Amount.Should().Be(0);
        money.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Add_WithSameCurrency_ShouldAddAmounts()
    {
        var money1 = Money.Create(50m);
        var money2 = Money.Create(30m);

        var result = money1.Add(money2);

        result.Amount.Should().Be(80m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Add_WithDifferentCurrency_ShouldThrowException()
    {
        var money1 = Money.Create(50m, "USD");
        var money2 = Money.Create(30m, "EUR");

        var act = () => money1.Add(money2);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*different currencies*");
    }

    [Fact]
    public void Subtract_WithSameCurrency_ShouldSubtractAmounts()
    {
        var money1 = Money.Create(50m);
        var money2 = Money.Create(30m);

        var result = money1.Subtract(money2);

        result.Amount.Should().Be(20m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Subtract_WithDifferentCurrency_ShouldThrowException()
    {
        var money1 = Money.Create(50m, "USD");
        var money2 = Money.Create(30m, "EUR");

        var act = () => money1.Subtract(money2);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*different currencies*");
    }

    [Fact]
    public void Subtract_WhenResultWouldBeNegative_ShouldThrowException()
    {
        var money1 = Money.Create(30m);
        var money2 = Money.Create(50m);

        var act = () => money1.Subtract(money2);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*negative*");
    }

    [Fact]
    public void Multiply_ShouldMultiplyAmount()
    {
        var money = Money.Create(25m);

        var result = money.Multiply(3);

        result.Amount.Should().Be(75m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Equals_WithSameValues_ShouldReturnTrue()
    {
        var money1 = Money.Create(100m, "USD");
        var money2 = Money.Create(100m, "USD");

        money1.Should().Be(money2);
    }

    [Fact]
    public void Equals_WithDifferentAmounts_ShouldReturnFalse()
    {
        var money1 = Money.Create(100m, "USD");
        var money2 = Money.Create(50m, "USD");

        money1.Should().NotBe(money2);
    }

    [Fact]
    public void Equals_WithDifferentCurrencies_ShouldReturnFalse()
    {
        var money1 = Money.Create(100m, "USD");
        var money2 = Money.Create(100m, "EUR");

        money1.Should().NotBe(money2);
    }

    [Fact]
    public void ToString_ShouldReturnFormattedString()
    {
        var money = Money.Create(25.50m, "USD");

        var result = money.ToString();

        result.Should().Be("25.50 USD");
    }
}
