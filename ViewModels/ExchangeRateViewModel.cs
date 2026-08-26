using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using System.Collections.ObjectModel;
using ExpenseTracker.Services.Interfaces;

namespace ExpenseTracker.ViewModels
{
    public partial class ExchangeRatesViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;

        [ObservableProperty]
        public partial ObservableCollection<ExchangeRate> Rates { get; set; } = new();

        // Pola formularza
        [ObservableProperty]
        public partial string SelectedSourceCurrency { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string SelectedTargetCurrency { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string RateText { get; set; } = string.Empty;

        public ObservableCollection<string> AvailableCurrencies { get; } = new();

        [ObservableProperty]
        public partial DateTime SelectedDate { get; set; } = DateTime.Today;

        public ExchangeRatesViewModel(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task LoadDataAsync()
        {
            var ratesFromDb = await _databaseService.GetExchangeRatesAsync();

            var tempRates = new ObservableCollection<ExchangeRate>();
            foreach (var rate in ratesFromDb)
            {
                tempRates.Add(rate);
            }
            Rates = tempRates; // Bezpieczna podmiana dla Windowsa

            AvailableCurrencies.Clear();
            // Używamy nowej, pięknej metody
            foreach (var c in Helpers.CurrencyHelper.GetSortedCurrencyDisplayList())
                AvailableCurrencies.Add(c);
        }

        [RelayCommand]
        private async Task SaveRateAsync()
        {
            // NOWOŚĆ: Dekodujemy piękne nazwy na surowe kody
            string? sourceCode = Helpers.CurrencyHelper.ExtractCode(SelectedSourceCurrency);
            string? targetCode = Helpers.CurrencyHelper.ExtractCode(SelectedTargetCurrency);

            // Jeśli wyciągnięty kod jest null (bo użytkownik kliknął linię oddzielającą "────" lub nic), przerywamy
            if (string.IsNullOrWhiteSpace(sourceCode) || string.IsNullOrWhiteSpace(targetCode))
                return;

            // NOWOŚĆ: Normalizujemy znak dziesiętny - zamieniamy przecinki na kropki
            string normalizedRate = RateText.Replace(',', '.');

            // Parsujemy twardo z użyciem InvariantCulture (które zawsze oczekuje kropki)
            if (!decimal.TryParse(normalizedRate, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal parsedRate) || parsedRate <= 0)
                return;

            var newRate = new ExchangeRate
            {
                SourceCurrency = sourceCode,
                TargetCurrency = targetCode,
                Rate = parsedRate,
                Date = SelectedDate
            };

            await _databaseService.SaveExchangeRateAsync(newRate);

            // Czyszczenie formularza i przeładowanie listy
          /*  SourceCurrencyText = string.Empty;
            TargetCurrencyText = string.Empty;*/
            RateText = string.Empty;
            SelectedDate = DateTime.Today;

            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task DeleteRateAsync(ExchangeRate rate)
        {
            if (rate == null) return;

            await _databaseService.DeleteExchangeRateAsync(rate);
            await LoadDataAsync();
        }

        // Magiczny mechanizm: odpala się, gdy próbujesz zmienić walutę źródłową
        partial void OnSelectedSourceCurrencyChanged(string oldValue, string newValue)
        {
            if (newValue != null && newValue.Contains("──"))
            {
                // MainThread pozwala na bezpieczną manipulację interfejsem w locie
                MainThread.BeginInvokeOnMainThread(() => SelectedSourceCurrency = oldValue);
            }
        }

        // To samo dla waluty docelowej
        partial void OnSelectedTargetCurrencyChanged(string oldValue, string newValue)
        {
            if (newValue != null && newValue.Contains("──"))
            {
                MainThread.BeginInvokeOnMainThread(() => SelectedTargetCurrency = oldValue);
            }
        }
    }
}