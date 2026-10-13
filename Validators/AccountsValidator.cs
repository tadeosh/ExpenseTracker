using ExpenseTracker.Resources.Strings;
using ExpenseTracker.ViewModels;
using FluentValidation;
using System.Globalization;

namespace ExpenseTracker.Validators
{
    public class AccountsValidator : AbstractValidator<AccountsViewModel>
    {
        public AccountsValidator()
        {
            RuleFor(x => x.AccountName)
                .NotEmpty()
                .WithMessage(AppResources.AccountValidationMissingData ?? "Wprowadź nazwę konta.");

            RuleFor(x => x.AccountCurrency)
                .NotEmpty()
                .WithMessage(AppResources.AccountValidationMissingData ?? "Wybierz walutę.");

            RuleFor(x => x.AccountBalance)
                .Must(BeAValidDecimal)
                .WithMessage(AppResources.AccountValidationInvalidAmount ?? "Wprowadź prawidłową kwotę początkową.");

            RuleFor(x => x.InitialBalanceDate)
                .NotNull()
                .WithMessage(AppResources.AccountValidationMissingInitialBalanceDate ?? "Wybierz datę salda początkowego.");

            RuleFor(x => x.InitialBalanceDate)
                .Must(date => !date.HasValue || date.Value.Date <= DateTime.Today)
                .WithMessage(AppResources.AccountValidationFutureInitialBalanceDate ?? "Data salda początkowego nie może być późniejsza niż dzisiaj.");
        }

        private static bool BeAValidDecimal(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var normalized = text.Replace(',', '.');

            return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
        }
    }
}
