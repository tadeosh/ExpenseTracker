using FluentValidation;
using ExpenseTracker.ViewModels;
using ExpenseTracker.Resources.Strings;
using System.Globalization;

namespace ExpenseTracker.Validators
{
    public class AccountsValidator : AbstractValidator<AccountsViewModel>
    {
        public AccountsValidator()
        {
            // Walidacja nazwy konta
            RuleFor(x => x.AccountName)
                .NotEmpty()
                .WithMessage(AppResources.AccountValidationMissingData ?? "Wprowadź nazwę konta.");

            // Walidacja waluty
            RuleFor(x => x.AccountCurrency)
                .NotEmpty()
                .WithMessage(AppResources.AccountValidationMissingData ?? "Wybierz walutę.");

            // Walidacja salda początkowego
            // W przeciwieństwie do transakcji saldo konta może być zerowe lub ujemne (np. karta kredytowa)
            RuleFor(x => x.AccountBalance)
                .Must(BeAValidDecimal)
                .WithMessage(AppResources.AccountValidationInvalidAmount ?? "Wprowadź prawidłową kwotę początkową.");
        }

        private bool BeAValidDecimal(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            string normalized = text.Replace(',', '.');
            // Zauważ brak "&& value > 0" - saldo może być ujemne!
            return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
        }
    }
}