using CommunityToolkit.Mvvm.ComponentModel;
using ExpenseTracker.Helpers;
using System.Collections.ObjectModel;
using ExpenseTracker.Services.Interfaces;

namespace ExpenseTracker.ViewModels
{
    public partial class FavoriteCurrenciesViewModel : ObservableObject
    {
        public ObservableCollection<CurrencyItem> Currencies { get; } = new();

        public void LoadData()
        {
            var favs = CurrencyHelper.GetFavoriteCurrencies();
            Currencies.Clear();

            // Używamy nowej, bezpiecznej właściwości AllCurrencyCodes
            foreach (var code in CurrencyHelper.AllCurrencyCodes.OrderBy(c => c))
            {
                Currencies.Add(new CurrencyItem
                {
                    Code = code,                                       // "PLN" - do zapisywania w pamięci
                    DisplayValue = CurrencyHelper.FormatDisplay(code), // "🇵🇱 PLN - Polski Złoty" - dla oczu użytkownika
                    IsFavorite = favs.Contains(code)
                });
            }
        }

        public void SaveFavorites()
        {
            // Zapisujemy tylko surowe kody!
            var favsToSave = Currencies.Where(c => c.IsFavorite).Select(c => c.Code);
            CurrencyHelper.SaveFavoriteCurrencies(favsToSave);
        }
    }

    public partial class CurrencyItem : ObservableObject
    {
        [ObservableProperty]
        public partial string Code { get; set; }

        // NOWOŚĆ: Piękny tekst dla interfejsu XAML
        [ObservableProperty]
        public partial string DisplayValue { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsFavorite { get; set; }
    }
}