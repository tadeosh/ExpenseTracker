using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Resources.Strings;
using System.Collections.ObjectModel;

namespace ExpenseTracker.ViewModels
{
    [QueryProperty(nameof(PreselectedAccountId), "PreselectedAccountId")]
    [QueryProperty(nameof(PreselectedCategoryId), "PreselectedCategoryId")] // NOWOŚĆ
    [QueryProperty(nameof(PreselectedProjectId), "PreselectedProjectId")] // NOWOŚĆ
    public partial class AddTransactionViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        // NOWOŚĆ: Zmienna przechowująca przekazane z zewnątrz ID konta
        [ObservableProperty]
        public partial string? PreselectedAccountId { get; set; }

        // NOWOŚĆ: Nowe parametry dla filtrów
        [ObservableProperty]
        public partial string? PreselectedCategoryId { get; set; }

        [ObservableProperty]
        public partial string? PreselectedProjectId { get; set; }

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
            // Trik inżynierski: Zapamiętujemy ID tego, co użytkownik już wybrał,
            // żeby po odświeżeniu list nic mu nie zniknęło z formularza!
            int? savedAccountId = SelectedAccount?.Id;
            int? savedDestAccountId = DestinationAccount?.Id;
            int? savedProjectId = SelectedProject?.Id;
            int? savedCategoryId = SelectedCategory?.Id;

            // Ładowanie z bazy
            var accountsFromDb = await _databaseService.GetAccountsAsync();
            Accounts.Clear();
            foreach (var acc in accountsFromDb) Accounts.Add(acc);

            var projectsFromDb = await _databaseService.GetProjectsAsync();
            Projects.Clear();
            foreach (var proj in projectsFromDb) Projects.Add(proj);

            _allCategories = await _databaseService.GetCategoriesAsync();
            var mainCats = _allCategories.Where(c => c.ParentId == null).OrderBy(c => c.DisplayOrder).ToList();

            MainCategories.Clear();
            foreach (var cat in mainCats)
            {
                var subCount = _allCategories.Count(c => c.ParentId == cat.Id);
                MainCategories.Add(new CategoryDisplayItem { Category = cat, SubcategoriesCount = subCount });
            }

            // ODTWARZANIE WYBORÓW (szukamy nowych obiektów na świeżych listach po ich ID)
            if (savedAccountId.HasValue)
            {
                SelectedAccount = Accounts.FirstOrDefault(a => a.Id == savedAccountId.Value);
            }
            // ZMIANA: Sprawdzamy, czy tekst nie jest pusty i próbujemy zamienić go na liczbę (int.TryParse)
            else if (!string.IsNullOrEmpty(PreselectedAccountId) && int.TryParse(PreselectedAccountId, out int preselectedId))
            {
                SelectedAccount = Accounts.FirstOrDefault(a => a.Id == preselectedId);
            }

            // 2. Odtwarzanie Projektu
            if (!string.IsNullOrEmpty(PreselectedProjectId) && int.TryParse(PreselectedProjectId, out int projId))
            {
                SelectedProject = Projects.FirstOrDefault(p => p.Id == projId);
            }

            // 3. Odtwarzanie Kategorii (Szukamy jej na pełnej liście)
            if (!string.IsNullOrEmpty(PreselectedCategoryId) && int.TryParse(PreselectedCategoryId, out int catId))
            {
                // Wystarczy przeszukać _allCategories, która zawiera absolutnie wszystkie główne i podkategorie!
                SelectedCategory = _allCategories.FirstOrDefault(c => c.Id == catId);
            }

            if (savedDestAccountId.HasValue) DestinationAccount = Accounts.FirstOrDefault(a => a.Id == savedDestAccountId.Value);
            if (savedProjectId.HasValue) SelectedProject = Projects.FirstOrDefault(p => p.Id == savedProjectId.Value);

            if (savedCategoryId.HasValue)
            {
                SelectedCategory = _allCategories.FirstOrDefault(c => c.Id == savedCategoryId.Value);
                if (SelectedCategory != null)
                {
                    SelectedCategoryName = SelectedCategory.Name;
                    SelectedCategoryColorHex = SelectedCategory.ColorHex;
                }
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
                // NOWOŚĆ: Dodajemy samą kategorię główną na samą górę listy podkategorii (prawa strona)!
                // Dzięki temu użytkownik może kliknąć ją po prawej stronie, by przypisać wydatek "ogólnie".
                SubCategories.Add(new CategoryDisplayItem { Category = value.Category });

                // Następnie ładujemy resztę faktycznych podkategorii
                foreach (var sub in subs) SubCategories.Add(new CategoryDisplayItem { Category = sub });
            }
            else
            {
                // Jeśli nie ma dzieci, po prostu wybieramy ją od razu i zamykamy okienko
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

        partial void OnSelectedCategoryChanged(Category? value)
        {
            if(value != null) ConfirmCategorySelection(value);
        }

        // ==========================================
        // ZAPIS TRANSAKCJI + UCZENIE KURSÓW
        // ==========================================

        // NOWOŚĆ: Kuloodporna metoda zamykająca formularz
        private async Task CloseFormSafeAsync()
        {
            // Sprawdzamy, czy formularz został otwarty jako "nakładka" (na stosie jest więcej niż 1 strona)
            if (Shell.Current.Navigation.NavigationStack.Count > 1)
            {
                await Shell.Current.Navigation.PopAsync();
            }
            else
            {
                // Jeśli stos jest pusty (otwarto absolutnie np. z menu), wymuszamy powrót na stronę główną
                await Shell.Current.GoToAsync("//HomePage");
            }
        }

        [RelayCommand]
        private async Task SaveTransactionAsync()
        {
            // Normalizujemy znak dziesiętny - zamieniamy przecinki na kropki
            string normalizedAmount = AmountText.Replace(',', '.');

            // Parsujemy twardo, niezależnie od języka systemu
            if (!decimal.TryParse(normalizedAmount, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
                return;

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
            await CloseFormSafeAsync();
            //await Shell.Current.GoToAsync("..");
           // await Shell.Current.Navigation.PopAsync();
        }

        // NOWOŚĆ: Komenda dla przycisku Anuluj / Strzałki Wstecz
        [RelayCommand]
        private async Task CancelAsync()
        {
            bool goBackToTransactions = !string.IsNullOrEmpty(PreselectedAccountId);

            ClearForm();
            await CloseFormSafeAsync();
            //await Shell.Current.GoToAsync("..");
            //await Shell.Current.Navigation.PopAsync();

        }

        // NOWOŚĆ: Wydzielone czyszczenie formularza
        private void ClearForm()
        {
            AmountText = string.Empty;
            DescriptionText = string.Empty;
            SelectedAccount = null;
            DestinationAccount = null;
            SelectedCategory = null;
            SelectedCategoryName = AppResources.CategoryLabel ?? "Kategoria";
            SelectedCategoryColorHex = "Transparent";
            SelectedProject = null;
            ExchangeRateText = string.Empty;

            // NOWOŚĆ: Resetujemy przekazany parametr po wyjściu z formularza
            PreselectedAccountId = null;
            PreselectedCategoryId = null;
            PreselectedProjectId = null;
        }

        [RelayCommand]
        private void SetTransactionType(string typeIndex)
        {
            if (int.TryParse(typeIndex, out int index))
            {
                SelectedTypeIndex = index;
            }
        }

        // NOWOŚĆ: Skoki do dodawania brakujących elementów
        [RelayCommand]
        private async Task GoToCategoriesAsync()
        {
            // Otwiera podstronę na wierzchu stosu
            await Shell.Current.GoToAsync("CategoriesPage");
        }

        [RelayCommand]
        private async Task GoToProjectsAsync()
        {
            await Shell.Current.GoToAsync("ProjectsPage");
        }
    }
}