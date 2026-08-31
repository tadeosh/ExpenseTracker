using FluentValidation;
using ExpenseTracker.ViewModels;
using ExpenseTracker.Resources.Strings;
using System.Globalization;

namespace ExpenseTracker.Validators
{
    public class AddTransactionValidator : AbstractValidator<AddTransactionViewModel>
    {
        public AddTransactionValidator()
        {
            // Walidacja kwoty
            RuleFor(x => x.AmountText)
                .Must(BeAValidPositiveDecimal)
                .WithMessage(AppResources.AmountInvalidMsg); // Z zasobów: "Wprowadź prawidłową kwotę..."

            // Walidacja konta źródłowego
            RuleFor(x => x.SelectedAccount)
                .NotNull()
                .WithMessage(AppResources.AccountRequiredMsg); // Z zasobów: "Wybierz konto."

            // Walidacja konta docelowego (Tylko dla przelewów)
            RuleFor(x => x.DestinationAccount)
                .NotNull().When(x => x.IsTransfer)
                .WithMessage(AppResources.TransferAccountInvalidMsg) // Z zasobów: "Wybierz prawidłowe konto..."
                .Must((vm, dest) => dest?.Id != vm.SelectedAccount?.Id)
                .When(x => x.IsTransfer && x.SelectedAccount != null)
                .WithMessage(AppResources.TransferAccountInvalidMsg);

            // Walidacja kursu walut (Tylko przy konwersji)
            RuleFor(x => x.ExchangeRateText)
                .Must(BeAValidPositiveDecimal).When(x => x.IsCurrencyConversion)
                .WithMessage(AppResources.ExchangeRateInvalidMsg); // Z zasobów: "Wprowadź prawidłowy kurs..."
        }

        private bool BeAValidPositiveDecimal(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string normalized = text.Replace(',', '.');
            return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value) && value > 0;
        }
    }
}