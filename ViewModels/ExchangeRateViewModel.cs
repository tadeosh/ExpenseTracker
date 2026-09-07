using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Resources.Strings;
using ExpenseTracker.Services.Interfaces;
using FluentValidation;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ExpenseTracker.ViewModels
{
    public partial class ExchangeRatesViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;
        private readonly IValidator<ExchangeRatesViewModel> _validator;

        // ZMIANA: Używamy naszego nowego Wrappera
        public ObservableCollection<ExchangeRateDisplayItem> Rates { get; } = new();

        public ObservableCollection<string> AvailableCurrencies { get; } = new();

        // --- STAN FORMULARZA ---
        [ObservableProperty] public partial bool IsFormVisible { get; set; } = false;
        [ObservableProperty] public partial bool IsEditing { get; set; } = false;

        [ObservableProperty] public partial string SelectedSourceCurrency { get; set; } = string.Empty;
        [ObservableProperty] public partial string SelectedTargetCurrency { get; set; } = string.Empty;
        [ObservableProperty] public partial string RateText { get; set; } = string.Empty;
        [ObservableProperty] public partial DateTime SelectedDate { get; set; } = DateTime.Today;

       
        [ObservableProperty] public partial string? FormError { get; set; }

        private ExchangeRateDisplayItem? _rateBeingEdited;

        public ExchangeRatesViewModel(IDatabaseService databaseService, IValidator<ExchangeRatesViewModel> validator)
        {
            _databaseService = databaseService;
            _validator = validator;
        }

        public async Task LoadDataAsync()
        {
            var ratesFromDb = await _databaseService.GetExchangeRatesAsync();

            Rates.Clear();
            foreach (var rate in ratesFromDb)
            {
                Rates.Add(new ExchangeRateDisplayItem { ExchangeRate = rate });
            }

            if (!AvailableCurrencies.Any())
            {
                foreach (var c in Helpers.CurrencyHelper.GetSortedCurrencyDisplayList())
                    AvailableCurrencies.Add(c);
            }
        }

        // --- ZARZĄDZANIE FORMULARZEM ---
        private void ClearErrors() => FormError = null;

        [RelayCommand]
        private void OpenAddForm()
        {
            if (IsFormVisible && !IsEditing)
            {
                IsFormVisible = false;
            }
            else
            {
                ClearErrors();
                IsEditing = false;
                _rateBeingEdited = null;
                RateText = string.Empty;
                SelectedDate = DateTime.Today;
                IsFormVisible = true;
            }
        }

        [RelayCommand]
        private void EditRate(ExchangeRateDisplayItem itemToEdit)
        {
            ClearErrors();
            IsFormVisible = true;
            IsEditing = true;
            _rateBeingEdited = itemToEdit;

            RateText = itemToEdit.ExchangeRate.Rate.ToString("0.####", CultureInfo.InvariantCulture);
            SelectedDate = itemToEdit.ExchangeRate.Date;

            // Dopasowanie Pickera do istniejących wartości (Wymaga dopasowania kodu waluty do wyświetlanej nazwy)
            SelectedSourceCurrency = AvailableCurrencies.FirstOrDefault(c => c.Contains(itemToEdit.ExchangeRate.SourceCurrency)) ?? string.Empty;
            SelectedTargetCurrency = AvailableCurrencies.FirstOrDefault(c => c.Contains(itemToEdit.ExchangeRate.TargetCurrency)) ?? string.Empty;
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditing = false;
            _rateBeingEdited = null;
            RateText = string.Empty;
            IsFormVisible = false;
            ClearErrors();
        }

        // --- ZAPIS ---
        [RelayCommand]
        private async Task SaveRateAsync()
        {
            ClearErrors();

            var validationResult = await _validator.ValidateAsync(this);
            if (!validationResult.IsValid)
            {
                // LINQ: Pobieramy same komunikaty i sklejamy je enterem
                FormError = string.Join(Environment.NewLine, validationResult.Errors.Select(e => e.ErrorMessage));
                return;
            }

            string sourceCode = Helpers.CurrencyHelper.ExtractCode(SelectedSourceCurrency)!;
            string targetCode = Helpers.CurrencyHelper.ExtractCode(SelectedTargetCurrency)!;
            decimal parsedRate = decimal.Parse(RateText.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);

            if (IsEditing && _rateBeingEdited != null)
            {
                _rateBeingEdited.ExchangeRate.SourceCurrency = sourceCode;
                _rateBeingEdited.ExchangeRate.TargetCurrency = targetCode;
                _rateBeingEdited.ExchangeRate.Rate = parsedRate;
                _rateBeingEdited.ExchangeRate.Date = SelectedDate;

                await _databaseService.SaveExchangeRateAsync(_rateBeingEdited.ExchangeRate);
            }
            else
            {
                var newRate = new ExchangeRate
                {
                    SourceCurrency = sourceCode,
                    TargetCurrency = targetCode,
                    Rate = parsedRate,
                    Date = SelectedDate
                };
                await _databaseService.SaveExchangeRateAsync(newRate);
            }

            CancelEdit();
            await LoadDataAsync();
        }

        // --- USUWANIE ---
        [RelayCommand]
        private void ToggleDeleteMode(ExchangeRateDisplayItem item)
        {
            // Zamykamy inne otwarte wiersze
            foreach (var rate in Rates.Where(r => r != item)) rate.IsDeleteMode = false;
            item.IsDeleteMode = !item.IsDeleteMode;
        }

        [RelayCommand]
        private async Task DeleteRateAsync(ExchangeRateDisplayItem item)
        {
            if (item == null) return;

            await _databaseService.DeleteExchangeRateAsync(item.ExchangeRate);
            Rates.Remove(item); // O(1) aktualizacja UI bez uderzania do bazy
        }

        // --- HACKI KONTROLEK UI ---
        partial void OnSelectedSourceCurrencyChanged(string oldValue, string newValue)
        {
            if (newValue != null && newValue.Contains("──"))
                MainThread.BeginInvokeOnMainThread(() => SelectedSourceCurrency = oldValue);
        }

        partial void OnSelectedTargetCurrencyChanged(string oldValue, string newValue)
        {
            if (newValue != null && newValue.Contains("──"))
                MainThread.BeginInvokeOnMainThread(() => SelectedTargetCurrency = oldValue);
        }
    }

    //============= KLASA WRAPPERA =================    
    public partial class ExchangeRateDisplayItem : ObservableObject
    {
        public ExchangeRate ExchangeRate { get; set; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MinusRotation))]
        public partial bool IsDeleteMode { get; set; }

        public double MinusRotation => IsDeleteMode ? 90 : 0;
    }
}