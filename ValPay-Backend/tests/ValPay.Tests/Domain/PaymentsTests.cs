using FluentAssertions;
using Xunit;
using ValPay.Domain;

namespace ValPay.Tests.Domain;

public class PaymentsTests
{
    [Fact]
    public void Transaction_Should_Create_With_Valid_Parameters()
    {
        // Arrange
        var id = new TransactionId(Guid.NewGuid());
        var merchantRef = new MerchantReference("REF-123");
        var money = new Money(1000, "USD");
        var status = TransactionStatus.Pending;

        // Act
        var transaction = new Transaction(id, merchantRef, money, status);

        // Assert
        transaction.Id.Should().Be(id);
        transaction.MerchantReference.Should().Be(merchantRef);
        transaction.Money.Should().Be(money);
        transaction.Status.Should().Be(status);
        transaction.PspReference.Should().BeNull();
    }

    [Fact]
    public void Transaction_Should_Create_With_PspReference()
    {
        // Arrange
        var id = new TransactionId(Guid.NewGuid());
        var merchantRef = new MerchantReference("REF-123");
        var money = new Money(1000, "USD");
        var status = TransactionStatus.Authorised;
        var pspRef = "PSP-123456";

        // Act
        var transaction = new Transaction(id, merchantRef, money, status, pspRef);

        // Assert
        transaction.PspReference.Should().Be(pspRef);
    }

    [Fact]
    public void Money_Should_Handle_Different_Currencies()
    {
        // Arrange & Act
        var usd = new Money(1000, "USD");
        var eur = new Money(850, "EUR");
        var gbp = new Money(750, "GBP");

        // Assert
        usd.Currency.Should().Be("USD");
        usd.AmountMinor.Should().Be(1000);
        eur.Currency.Should().Be("EUR");
        eur.AmountMinor.Should().Be(850);
        gbp.Currency.Should().Be("GBP");
        gbp.AmountMinor.Should().Be(750);
    }
}

