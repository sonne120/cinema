using Cinema.Domain.Common.Models;
using Cinema.Domain.PaymentAggregate;
using Cinema.Domain.PaymentAggregate.Events;
using Cinema.Domain.PaymentAggregate.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Cinema.Domain.UnitTests.PaymentAggregate;

public class PaymentTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreatePayment()
    {
        var reservationId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var amount = Money.Create(50m);

        var result = Payment.Create(reservationId, customerId, amount, PaymentMethod.CreditCard);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReservationId.Should().Be(reservationId);
        result.Value.CustomerId.Should().Be(customerId);
        result.Value.Amount.Should().Be(amount);
        result.Value.Method.Should().Be(PaymentMethod.CreditCard);
        result.Value.Status.Should().Be(PaymentStatus.Pending);
        result.Value.DomainEvents.Should().ContainSingle(e => e is PaymentCreatedEvent);
    }

    [Fact]
    public void Create_WithEmptyReservationId_ShouldReturnFailure()
    {
        var amount = Money.Create(50m);

        var result = Payment.Create(Guid.Empty, Guid.NewGuid(), amount, PaymentMethod.CreditCard);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Reservation ID");
    }

    [Fact]
    public void Create_WithZeroAmount_ShouldReturnFailure()
    {
        var amount = Money.Zero();

        var result = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), amount, PaymentMethod.CreditCard);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("positive");
    }

    [Fact]
    public void StartProcessing_FromPending_ShouldSucceed()
    {
        var payment = CreateValidPayment();

        var result = payment.StartProcessing();

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Processing);
    }

    [Fact]
    public void StartProcessing_FromNonPending_ShouldFail()
    {
        var payment = CreateValidPayment();
        payment.StartProcessing();

        var result = payment.StartProcessing();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Complete_FromProcessing_ShouldSucceed()
    {
        var payment = CreateValidPayment();
        payment.StartProcessing();

        var result = payment.Complete("TXN-12345");

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Completed);
        payment.TransactionId.Should().Be("TXN-12345");
        payment.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_FromPending_ShouldFail()
    {
        var payment = CreateValidPayment();

        var result = payment.Complete("TXN-12345");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Complete_WithEmptyTransactionId_ShouldFail()
    {
        var payment = CreateValidPayment();
        payment.StartProcessing();

        var result = payment.Complete("");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Transaction ID");
    }

    [Fact]
    public void Decline_FromProcessing_ShouldSucceed()
    {
        var payment = CreateValidPayment();
        payment.StartProcessing();

        var result = payment.Decline("Insufficient funds");

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Declined);
        payment.FailureReason.Should().Be("Insufficient funds");
    }

    [Fact]
    public void Fail_FromPending_ShouldSucceed()
    {
        var payment = CreateValidPayment();

        var result = payment.Fail("Gateway timeout");

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().Be("Gateway timeout");
    }

    [Fact]
    public void Fail_FromCompleted_ShouldFail()
    {
        var payment = CreateCompletedPayment();

        var result = payment.Fail("Some reason");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Refund_FromCompleted_ShouldSucceed()
    {
        var payment = CreateCompletedPayment();

        var result = payment.Refund("Customer request");

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.RefundedAmount.Should().Be(payment.Amount);
        payment.RefundedAt.Should().NotBeNull();
    }

    [Fact]
    public void Refund_FromPending_ShouldFail()
    {
        var payment = CreateValidPayment();

        var result = payment.Refund("Customer request");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void PartialRefund_ShouldRefundPartialAmount()
    {
        var payment = CreateCompletedPayment();
        var refundAmount = Money.Create(20m);

        var result = payment.PartialRefund(refundAmount, "Partial refund");

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.PartiallyRefunded);
        payment.RefundedAmount.Amount.Should().Be(20m);
    }

    [Fact]
    public void PartialRefund_ExceedingAmount_ShouldFail()
    {
        var payment = CreateCompletedPayment();
        var refundAmount = Money.Create(100m);

        var result = payment.PartialRefund(refundAmount, "Too much");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("exceeds");
    }

    [Fact]
    public void CanBeRefunded_WhenCompleted_ShouldReturnTrue()
    {
        var payment = CreateCompletedPayment();

        payment.CanBeRefunded().Should().BeTrue();
    }

    [Fact]
    public void CanBeRefunded_WhenPending_ShouldReturnFalse()
    {
        var payment = CreateValidPayment();

        payment.CanBeRefunded().Should().BeFalse();
    }

    [Fact]
    public void GetRefundableAmount_ShouldReturnAmountMinusRefunded()
    {
        var payment = CreateCompletedPayment();
        payment.PartialRefund(Money.Create(20m), "Partial");

        var refundable = payment.GetRefundableAmount();

        refundable.Amount.Should().Be(30m);
    }

    private static Payment CreateValidPayment()
    {
        var result = Payment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Money.Create(50m),
            PaymentMethod.CreditCard);
        return result.Value;
    }

    private static Payment CreateCompletedPayment()
    {
        var payment = CreateValidPayment();
        payment.StartProcessing();
        payment.Complete("TXN-12345");
        return payment;
    }
}

public class PaymentIdTests
{
    [Fact]
    public void CreateUnique_ShouldCreateNewId()
    {
        var id1 = PaymentId.CreateUnique();
        var id2 = PaymentId.CreateUnique();

        id1.Should().NotBe(id2);
    }

    [Fact]
    public void Create_WithGuid_ShouldWrapGuid()
    {
        var guid = Guid.NewGuid();

        var id = PaymentId.Create(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void ImplicitConversion_ToGuid_ShouldWork()
    {
        var id = PaymentId.CreateUnique();

        Guid guid = id;

        guid.Should().Be(id.Value);
    }
}
