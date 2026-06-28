using FluentValidation;
using MoneyTransferService.Business.Requests;

namespace MoneyTransferService.Business.Validators;

public sealed class TransferCommandValidator : AbstractValidator<TransferCommand>
{
    public TransferCommandValidator()
    {
        RuleFor(x => x.SenderIban)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Sender IBAN is required.")
            .Length(26)
            .WithMessage("Sender IBAN must be 26 characters long.");

        RuleFor(x => x.ReceiverIban)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Receiver IBAN is required.")
            .Length(26)
            .WithMessage("Receiver IBAN must be 26 characters long.")
            .NotEqual(x => x.SenderIban)
            .WithMessage("Sender and receiver IBANs cannot be the same.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Transfer amount must be greater than zero.");

        RuleFor(x => x.CurrencyCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Currency code is required.")
            .Length(3)
            .WithMessage("Currency code must be 3 characters.")
            .Must(x => x == x.ToUpperInvariant())
            .WithMessage("Currency code must be uppercase.");

        RuleFor(x => x.IdempotencyKey)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Idempotency key is required.")
            .MaximumLength(100)
            .WithMessage("Idempotency key cannot exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage("Description cannot exceed 500 characters.");
    }
}
