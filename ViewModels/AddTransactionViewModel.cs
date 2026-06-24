using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Resources.Strings;
using System.Collections.ObjectModel;

namespace ExpenseTracker.ViewModels
{
    public partial class AddTransactionViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        // Listy wyboru dla Pickerów na ekranie
        public ObservableCollection<Account> Accounts { get; } = new();
        public ObservableCollection<Project> Projects { get; } = new();

        // NOWOŚĆ: Kolekcje i stany dla naszego "Fałszywego Pickera" kategorii
        public ObservableCollection<CategoryDisplayItem> MainCategories { get; } = new();
        public ObservableCollection<CategoryDisplayItem> SubCategories { get; } = new();
        private List<Category> _allCategories = new();

        [ObservableProperty]
        public partial bool IsCategoryDropdownOpen { get; set; } = false;

        [ObservableProperty]
        public partial string SelectedCategoryName { get; set; } = AppResources.CategoryLabel;

        [ObservableProperty]
        public partial string SelectedCategoryColorHex { get; set; } = "Transparent";

        [ObservableProperty]
        public partial CategoryDisplayItem? SelectedMainCategory { get; set; }

        [ObservableProperty]
        public partial CategoryDisplayItem? SelectedSubCategory { get; set; }

        // Ukryta zmienna do zapamiętania kursu z bazy (żeby wiedzieć, czy użytkownik go zmienił)
        private decimal? _lastFetchedRate = null;

        // Pola powiązane z formularzem wprowadzania
        [ObservableProperty]
        public partial string AmountText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial DateTime SelectedDate { get; set; } = DateTime.Today;

        [ObservableProperty]
        public partial string DescriptionText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial Account? SelectedAccount { get; set; }

        [ObservableProperty]
        public partial Category? SelectedCategory { get; set; }

        [ObservableProperty]
        public partial Project? SelectedProject { get; set; }

        [ObservableProperty]
        public partial int SelectedTypeIndex { get; set; } = 0; // Domyślnie wydatek

        [ObservableProperty]
        public partial Account? DestinationAccount { get; set; }

        [ObservableProperty]
        public partial string ExchangeRateText { get; set; } = string.Empty;

        // Flagi decydujące o tym, czy pola są widoczne na ekranie
        [ObservableProperty]
        public partial bool IsTransfer { get; set; }

        [ObservableProperty]
        public partial bool IsCurrencyConversion { get; set; }

        [ObservableProperty]
        public partial string CurrencyConversionLabel { get; set; } = string.Empty;

        public AddTransactionViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        // ==========================================
        // NASŁUCHIWACZE 
        // ==========================================

        partial void OnSelectedTypeIndexChanged(int value)
        {
            IsTransfer = value == 2;
            CheckCurrencyConversion();
        }

        partial void OnSelectedAccountChanged(Account? value) => CheckCurrencyConversion();
        partial void OnDestinationAccountChanged(Account? value) => CheckCurrencyConversion();
        partial void OnSelectedDateChanged(DateTime value) => CheckCurrencyConversion();

        // Inteligentna logika sprawdzająca i pobierająca kurs
        private async void CheckCurrencyConversion()
        {
            if (IsTransfer && SelectedAccount != null && DestinationAccount != null && SelectedAccount.Currency != DestinationAccount.Currency)
            {
                IsCurrencyConversion = true;
                CurrencyConversionLabel = $"{SelectedAccount.Currency} -> {DestinationAccount.Currency}";

                // Szukamy w bazie
                var rate = await _databaseService.GetApplicableExchangeRateAsync(
                    SelectedAccount.Currency,
                    DestinationAccount.Currency,
                    SelectedDate);

                if (rate.HasValue)
                {
                    ExchangeRateText = rate.Value.ToString("0.####");
                    _lastFetchedRate = rate.Value;
                }
                else
                {
                    ExchangeRateText = string.Empty;
                    _lastFetchedRate = null;
                }
            }
            else
            {
                IsCurrencyConversion = false;
                CurrencyConversionLabel = string.Empty;
                ExchangeRateText = string.Empty;
                _lastFetchedRate = null;
            }
        }

        // ==========================================
        // ŁADOWANIE DANYCH
        // ==========================================

        public async Task LoadDataAsync()
        {
            var accountsFromDb = await _databaseService.GetAccountsAsync();
            Accounts.Clear();
            foreach (var acc in accountsFromDb) Accounts.Add(acc);

            var projectsFromDb = await _databaseService.GetProjectsAsync();
            Projects.Clear();
            foreach (var proj in projectsFromDb) Projects.Add(proj);

            // NOWOŚĆ: Budowanie hierarchicznej listy kategorii z liczeniem podkategorii
            _allCategories = await _databaseService.GetCategoriesAsync();
            var mainCats = _allCategories.Where(c => c.ParentId == null).OrderBy(c => c.DisplayOrder).ToList();

            MainCategories.Clear();
            foreach (var cat in mainCats)
            {
                var subCount = _allCategories.Count(c => c.ParentId == cat.Id);
                MainCategories.Add(new CategoryDisplayItem
                {
                    Category = cat,
                    SubcategoriesCount = subCount
                });
            }
        }

        // ==========================================
        // OBSŁUGA WYBORU KATEGORII (Fałszywy Picker)
        // ==========================================

        [RelayCommand]
        private void ToggleCategoryDropdown()
        {
            IsCategoryDropdownOpen = !IsCategoryDropdownOpen;
            if (!IsCategoryDropdownOpen)
            {
                // Czyszczenie wyboru pośredniego po zamknięciu
                SelectedMainCategory = null;
                SelectedSubCategory = null;
                SubCategories.Clear();
            }
        }

        // Kiedy użytkownik kliknie Kategorię Główną po lewej stronie
        partial void OnSelectedMainCategoryChanged(CategoryDisplayItem? value)
        {
            if (value == null) return;

            SubCategories.Clear();
            var subs = _allCategories.Where(c => c.ParentId == value.Category.Id).OrderBy(c => c.DisplayOrder).ToList();

            if (subs.Any())
            {
                // Jeśli ma dzieci, ładujemy je po prawej stronie!
                foreach (var sub in subs) SubCategories.Add(new CategoryDisplayItem { Category = sub });
            }
            else
            {
                // Jeśli nie ma dzieci, po prostu wybieramy ją od razu
                ConfirmCategorySelection(value.Category);
            }
        }

        // Kiedy użytkownik kliknie Podkategorię po prawej stronie
        partial void OnSelectedSubCategoryChanged(CategoryDisplayItem? value)
        {
            if (value == null) return;
            ConfirmCategorySelection(value.Category);
        }

        private void ConfirmCategorySelection(Category category)
        {
            SelectedCategory = category;
            SelectedCategoryName = category.Name;
            SelectedCategoryColorHex = category.ColorHex;

            IsCategoryDropdownOpen = false;

            // Sprzątamy stan widoku
            SelectedMainCategory = null;
            SelectedSubCategory = null;
            SubCategories.Clear();
        }

        // ==========================================
        // ZAPIS TRANSAKCJI + UCZENIE KURSÓW
        // ==========================================

        [RelayCommand]
        private async Task SaveTransactionAsync()
        {
            if (!decimal.TryParse(AmountText, out decimal amount) || amount <= 0) return;
            //if (string.IsNullOrWhiteSpace(DescriptionText) || SelectedAccount == null || SelectedCategory == null) return;
            if (SelectedAccount == null) return;

            TransactionType type = SelectedTypeIndex switch
            {
                1 => TransactionType.Income,
                2 => TransactionType.Transfer,
                _ => TransactionType.Expense
            };

            if (type == TransactionType.Transfer)
            {
                if (DestinationAccount == null || SelectedAccount.Id == DestinationAccount.Id) return;
            }

            decimal? exchangeRate = null;
            if (IsCurrencyConversion)
            {
                if (!decimal.TryParse(ExchangeRateText, out decimal parsedRate) || parsedRate <= 0) return;
                exchangeRate = parsedRate;

                // --- AUTOMATYCZNE UCZENIE SIĘ KURSÓW WALUT ---
                if (_lastFetchedRate == null || _lastFetchedRate.Value != parsedRate)
                {
                    var newLearnedRate = new ExchangeRate
                    {
                        SourceCurrency = SelectedAccount.Currency,
                        TargetCurrency = DestinationAccount.Currency,
                        Rate = parsedRate,
                        Date = SelectedDate
                    };
                    await _databaseService.SaveExchangeRateAsync(newLearnedRate);
                }
            }

            var transaction = new Transaction
            {
                Amount = amount,
                Date = SelectedDate,
                Description = DescriptionText,
                Type = type,
                AccountId = SelectedAccount.Id,
                CategoryId = SelectedCategory?.Id,
                ProjectId = SelectedProject?.Id,
                DestinationAccountId = type == TransactionType.Transfer ? DestinationAccount?.Id : null,
                ExchangeRate = exchangeRate
            };

            await _databaseService.SaveTransactionAsync(transaction);

            // Czyszczenie formularza
            AmountText = string.Empty;
            DescriptionText = string.Empty;
            SelectedAccount = null;
            DestinationAccount = null;
            SelectedCategory = null;
            SelectedCategoryName = AppResources.CategoryLabel;
            SelectedCategoryColorHex = "Transparent";
            SelectedProject = null;
            ExchangeRateText = string.Empty;

            await Shell.Current.GoToAsync("//HomePage");
        }

        [RelayCommand]
        private void SetTransactionType(string typeIndex)
        {
            if (int.TryParse(typeIndex, out int index))
            {
                SelectedTypeIndex = index;
            }
        }
    }
}