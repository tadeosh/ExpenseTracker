using FluentValidation;
using ExpenseTracker.ViewModels;
using System.Globalization;
using ExpenseTracker.Resources.Strings;

namespace ExpenseTracker.Validators
{
    public class ExchangeRatesValidator : AbstractValidator<ExchangeRatesViewModel>
    {
        public ExchangeRatesValidator()
        {
            // 1. Walidacja waluty źródłowej
            RuleFor(x => x.SelectedSourceCurrency)
                .NotEmpty().WithMessage(AppResources.ValErrorSourceCurrency ?? "Wybierz walutę źródłową.")
                .Must(BeAValidSelection).WithMessage(AppResources.ValErrorInvalidCurrency ?? "Wybierz poprawną walutę.");

            // 2. Walidacja waluty docelowej i sprawdzenie, czy nie jest taka sama jak źródłowa
            RuleFor(x => x.SelectedTargetCurrency)
                .NotEmpty().WithMessage(AppResources.ValErrorTargetCurrency ?? "Wybierz walutę docelową.")
                .Must(BeAValidSelection).WithMessage(AppResources.ValErrorInvalidCurrency ?? "Wybierz poprawną walutę.")
                .NotEqual(x => x.SelectedSourceCurrency)
                .WithMessage(AppResources.ValErrorSameCurrency ?? "Waluty źródłowa i docelowa muszą się różnić.");

            // 3. Walidacja samego kursu (format i wartość)
            RuleFor(x => x.RateText)
                .NotEmpty().WithMessage(AppResources.ValErrorRateRequired ?? "Podaj kurs wymiany.")
                .Must(BeAValidPositiveDecimal).WithMessage(AppResources.ValErrorInvalidRate ?? "Kurs musi być prawidłową liczbą większą od zera.");
        }

        // --- Metody pomocnicze ---

        private bool BeAValidSelection(string? currencyText)
        {
            // Zabezpieczenie przed wybraniem linii oddzielającej (np. "────")
            if (string.IsNullOrWhiteSpace(currencyText)) return false;
            if (currencyText.Contains("──")) return false;

            return true;
        }

        private bool BeAValidPositiveDecimal(string? rateText)
        {
            if (string.IsNullOrWhiteSpace(rateText)) return false;

            string normalizedRate = rateText.Replace(',', '.');

            bool isValid = decimal.TryParse(
                normalizedRate,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out decimal parsedRate);

            return isValid && parsedRate > 0;
        }
    }
}