using FluentValidation;
using ValPay.Application;

namespace ValPay.Infrastructure.Validation;

public class PaymentMethodsRequestValidator : AbstractValidator<PaymentMethodsRequest>
{
    public PaymentMethodsRequestValidator()
    {
        RuleFor(x => x.AmountMinor)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .WithMessage("Currency must be a 3-character code");

        RuleFor(x => x.Country)
            .NotEmpty()
            .Length(2)
            .WithMessage("Country must be a 2-character code");

        RuleFor(x => x.MerchantAccount)
            .NotEmpty()
            .WithMessage("Merchant account is required");
    }
}

public class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.Reference)
            .NotEmpty()
            .MaximumLength(80)
            .WithMessage("Reference is required and must be less than 80 characters");

        RuleFor(x => x.AmountMinor)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .WithMessage("Currency must be a 3-character code");

        RuleFor(x => x.ReturnUrl)
            .NotEmpty()
            .Must(BeAValidUrl)
            .WithMessage("Return URL must be a valid URL");

        RuleFor(x => x.PaymentMethod)
            .NotNull()
            .WithMessage("Payment method is required");
    }

    private static bool BeAValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out _);
    }
}
