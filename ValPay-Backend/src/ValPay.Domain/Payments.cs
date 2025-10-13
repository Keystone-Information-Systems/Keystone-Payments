namespace ValPay.Domain;

public enum TransactionStatus { Pending, Authorised, Refused, Captured, Cancelled }

public record Money(long AmountMinor, string Currency);
public record TransactionId(Guid Value);
public record MerchantReference(string Value);

public record Transaction(
    TransactionId Id,
    MerchantReference MerchantReference,
    Money Money,
    TransactionStatus Status,
    string? PspReference = null);

