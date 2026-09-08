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
            When(x => x.IsRecurring, () =>
            {
                RuleFor(x => x.RecurrenceIntervalText)
                    .NotEmpty().WithMessage(AppResources.ValErrorIntervalRequired ?? "Podaj interwał (np. 1).")
                    .Must(BeAValidPositiveInt).WithMessage(AppResources.ValErrorIntervalInvalid ?? "Interwał musi być liczbą całkowitą większą od zera (np. 1, 2, 3).");

                When(x => x.HasEndDate, () =>
                {
                    RuleFor(x => x.EndDate.Date)
                        .GreaterThan(x => x.SelectedDate.Date)
                        .WithMessage(AppResources.ValErrorEndDateTooEarly ?? "Data zakończenia musi być późniejsza niż data pierwszej transakcji.");
                });
            });
        }

        private bool BeAValidPositiveDecimal(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string normalized = text.Replace(',', '.');
            return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value) && value > 0;
        }

        private bool BeAValidPositiveInt(string? intervalText)
        {
            if (string.IsNullOrWhiteSpace(intervalText)) return false;
            return int.TryParse(intervalText, out int parsed) && parsed > 0;
        }
    }
}