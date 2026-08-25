
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Resources.Strings;
using System.Collections.ObjectModel;
using ExpenseTracker.Services.Interfaces;


namespace ExpenseTracker.ViewModels
{
    // ZMIANA: Wracamy do najprostszego mechanizmu odbioru parametru
    [QueryProperty(nameof(AccountIdParam), "AccountId")]
    public partial class TransactionsViewModel : ObservableObject // <-- usunięto IQueryAttributable
    {
        private readonly IDatabaseService _databaseService;

        // NOWOŚĆ: Odbiera ID konta jako tekst (może być null, jeśli wejdziemy z menu głównego)
        [ObservableProperty]
        public partial string? AccountIdParam { get; set; }

        // NOWOŚĆ: Przechowuje faktyczną, odkodowaną liczbę (lub null, jeśli ładujemy wszystkie konta)
        private int? _parsedAccountId;

        // Przechowuje nazwę konta do wyświetlenia w nagłówku
        [ObservableProperty]
        public partial string AccountName { get; set; } = "...";

        // Kolekcja dla interfejsu (to, co widzi użytkownik po przefiltrowaniu i posortowaniu)
        public ObservableCollection<TransactionDisplayItem> Transactions { get; } = new();

        // Kopia zapasowa wszystkich transakcji (żeby nie odpytywać bazy przy każdym znaku wyszukiwania)
        private List<TransactionDisplayItem> _allTransactions = new();

        // --- FILTRY I WYSZUKIWANIE ---
        [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
        [ObservableProperty] public partial string MinAmountText { get; set; } = string.Empty;
        [ObservableProperty] public partial string MaxAmountText { get; set; } = string.Empty;

        public ObservableCollection<Category> AvailableCategories { get; } = new();
        public ObservableCollection<Project> AvailableProjects { get; } = new();

        [ObservableProperty] public partial Category? SelectedCategory { get; set; }
        [ObservableProperty] public partial Project? SelectedProject { get; set; }

        // --- SORTOWANIE ---
        // Te zmienne zostają jako prywatne pola, bo NIE MAJĄ atrybutu [ObservableProperty]
        private string _currentSortColumn = "Date";
        private bool _isAscending = false;

        // Ikonki sortowania dla nagłówków (▲ / ▼)
        [ObservableProperty] public partial string DateSortIcon { get; set; } = "▼";
        [ObservableProperty] public partial string CategorySortIcon { get; set; } = "";
        [ObservableProperty] public partial string DescriptionSortIcon { get; set; } = "";
        [ObservableProperty] public partial string ProjectSortIcon { get; set; } = "";
        [ObservableProperty] public partial string AmountSortIcon { get; set; } = "";
        [ObservableProperty] public partial string AccountSortIcon { get; set; } = "";

        // Flaga dla widoku: true = wszystkie transakcje, false = konkretne konto
        [ObservableProperty]
        public partial bool IsGlobalView { get; set; }

        // Lista kont dla pickera filtrów
        public ObservableCollection<Account> AvailableAccounts { get; } = new();

        // Wybrane konto w filtrze
        [ObservableProperty]
        public partial Account? SelectedAccount { get; set; }

        public TransactionsViewModel(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task LoadDataAsync()
        {
            try
            {
                // 1. ZAPAMIĘTUJEMY AKTUALNE FILTRY
                int? savedCategoryId = SelectedCategory?.Id;
                int? savedProjectId = SelectedProject?.Id;
                int? savedAccountIdFilter = SelectedAccount?.Id; // NOWOŚĆ

                // NOWOŚĆ: Dekodujemy ID konta
                _parsedAccountId = null;
                if (!string.IsNullOrEmpty(AccountIdParam) && int.TryParse(AccountIdParam, out int id))
                {
                    _parsedAccountId = id;
                }

                // NOWOŚĆ: Ustawiamy flagę IsGlobalView na true, jeśli nie przekazano _parsedAccountId
                IsGlobalView = !_parsedAccountId.HasValue;

                // Pobieranie danych z bazy
                var accounts = await _databaseService.GetAccountsAsync();
                var categories = await _databaseService.GetCategoriesAsync();
                var projects = await _databaseService.GetProjectsAsync();
                var rawTransactions = await _databaseService.GetTransactionsAsync();

                var categoryDict = categories.ToDictionary(x => x.Id);
                var projectDict = projects.ToDictionary(x => x.Id);
                var accountDict = accounts.ToDictionary(x => x.Id);


                // NOWOŚĆ: Dynamiczny tytuł strony
                if (_parsedAccountId.HasValue)
                {
                    var currentAccount = accounts.FirstOrDefault(a => a.Id == _parsedAccountId.Value);
                    if (currentAccount != null)
                    {
                        AccountName = $"{AppResources.TransactionsPageTitle}: {currentAccount.Name}";
                    }
                }
                else
                {
                    // Brak ID oznacza widok globalny
                    AccountName = AppResources.AllTransactionsTitle ?? "Wszystkie transakcje";
                }

                // Ładujemy pickery
                AvailableCategories.Clear();
                AvailableProjects.Clear();
                AvailableAccounts.Clear(); // NOWOŚĆ
                foreach (var c in categories) AvailableCategories.Add(c);
                foreach (var p in projects) AvailableProjects.Add(p);
                foreach (var a in accounts) AvailableAccounts.Add(a); // NOWOŚĆ

                // ODTWARZAMY FILTRY PO ZAŁADOWANIU
                if (savedCategoryId.HasValue)
                    SelectedCategory = AvailableCategories.FirstOrDefault(c => c.Id == savedCategoryId.Value);
                if (savedProjectId.HasValue)
                    SelectedProject = AvailableProjects.FirstOrDefault(p => p.Id == savedProjectId.Value);
                if (savedAccountIdFilter.HasValue) 
                    SelectedAccount = AvailableAccounts.FirstOrDefault(a => a.Id == savedAccountIdFilter.Value); // NOWOŚĆ

                // NOWOŚĆ: Filtrujemy transakcje tylko wtedy, gdy mamy konkretne konto
                var filteredTransactions = rawTransactions.AsEnumerable();
                if (_parsedAccountId.HasValue)
                {
                    filteredTransactions = filteredTransactions.Where(t => t.AccountId == _parsedAccountId.Value);
                }

                // Budujemy obiekty wyświetlane na liście
                _allTransactions = filteredTransactions
                    .Select(t => new TransactionDisplayItem
                    {
                        Transaction = t,
                        //CategoryName = categories.FirstOrDefault(c => c.Id == t.CategoryId)?.Name ?? "-",
                        CategoryName = categoryDict.TryGetValue(t.CategoryId ?? 0, out var category) ? category.Name : "-",
                        //ProjectName = projects.FirstOrDefault(p => p.Id == t.ProjectId)?.Name ?? "-",
                        ProjectName = categoryDict.TryGetValue(t.ProjectId ?? 0, out var project) ? project.Name : "-",
                        Description = t.Description ?? string.Empty,
                        // NOWOŚĆ: Dodajemy nazwę konta i flagę widoczności
                        //AccountName = accounts.FirstOrDefault(a => a.Id == t.AccountId)?.Name ?? "-",
                        AccountName = accountDict.TryGetValue(t.AccountId, out var account) ? account.Name : "-",
                        ShowAccount = IsGlobalView
                    }).ToList();

                ApplyFiltersAndSort();
            }
            catch (Exception ex)
            {
                // Jeśli coś pójdzie nie tak, wyłapiemy to tutaj zamiast cichego "craschu"
                System.Diagnostics.Debug.WriteLine($"Błąd podczas ładowania transakcji: {ex.Message}");
            }
        }

        // --- NASŁUCHIWACZE ZMIAN W FILTRACH ---
        partial void OnSearchTextChanged(string value) => ApplyFiltersAndSort();
        partial void OnMinAmountTextChanged(string value) => ApplyFiltersAndSort();
        partial void OnMaxAmountTextChanged(string value) => ApplyFiltersAndSort();
        partial void OnSelectedCategoryChanged(Category? value) => ApplyFiltersAndSort();
        partial void OnSelectedProjectChanged(Project? value) => ApplyFiltersAndSort();
        // Odśwież listę, gdy użytkownik zmieni filtr konta
        partial void OnSelectedAccountChanged(Account? value) => ApplyFiltersAndSort();

        // --- LOGIKA SORTOWANIA ---
        [RelayCommand]
        private void SortBy(string column)
        {
            if (_currentSortColumn == column)
            {
                _isAscending = !_isAscending; // Odwracamy kierunek
            }
            else
            {
                _currentSortColumn = column;
                _isAscending = true; // Domyślnie rosnąco dla nowej kolumny
            }

            UpdateSortIcons();
            ApplyFiltersAndSort();
        }

        [RelayCommand]
        private void ClearFilters()
        {
            SearchText = string.Empty;
            MinAmountText = string.Empty;
            MaxAmountText = string.Empty;
            SelectedCategory = null;
            SelectedProject = null;
            SelectedAccount = null;
        }

        [RelayCommand]
        private async Task NavigateToAddTransactionAsync()
        {
            // Budujemy listę parametrów do przekazania
            var queryParams = new List<string>();

            if (_parsedAccountId.HasValue)
                queryParams.Add($"PreselectedAccountId={_parsedAccountId.Value}");

            if (SelectedCategory != null)
                queryParams.Add($"PreselectedCategoryId={SelectedCategory.Id}");

            if (SelectedProject != null)
                queryParams.Add($"PreselectedProjectId={SelectedProject.Id}");

            // Sklejamy bezpieczny adres URL
            string url = "AddTransactionPage";
            if (queryParams.Any())
            {
                url += "?" + string.Join("&", queryParams);
            }

            await Shell.Current.GoToAsync(url);
        }

        private void UpdateSortIcons()
        {
            // Resetujemy wszystkie ikonki
            DateSortIcon = CategorySortIcon = DescriptionSortIcon = ProjectSortIcon = AmountSortIcon = AccountSortIcon = "";
            string icon = _isAscending ? "▲" : "▼";

            switch (_currentSortColumn)
            {
                case "Date": DateSortIcon = icon; break;
                case "Category": CategorySortIcon = icon; break;
                case "Description": DescriptionSortIcon = icon; break;
                case "Project": ProjectSortIcon = icon; break;
                case "Amount": AmountSortIcon = icon; break;
                case "Account": AccountSortIcon = icon; break;
            }
        }

        // --- GŁÓWNY SILNIK APLIKUJĄCY ZMIANY ---
        private void ApplyFiltersAndSort()
        {
            var query = _allTransactions.AsEnumerable();

            // 1. Wyszukiwanie tekstowe (po opisie, kategorii i projekcie)
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lowerSearch = SearchText.ToLower();
                query = query.Where(t =>
                    t.Description.ToLower().Contains(lowerSearch) ||
                    t.CategoryName.ToLower().Contains(lowerSearch) ||
                    t.ProjectName.ToLower().Contains(lowerSearch));                    
            }

            // 2. Filtry kwotowe (używamy normalizacji przecinka tak jak robiliśmy to wcześniej!)
            if (decimal.TryParse(MinAmountText?.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal min))
            {
                query = query.Where(t => t.Transaction.Amount >= min);
            }

            if (decimal.TryParse(MaxAmountText?.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal max))
            {
                query = query.Where(t => t.Transaction.Amount <= max);
            }

            // 3. Filtry obiektowe (Pickery)
            if (SelectedCategory != null)
                query = query.Where(t => t.Transaction.CategoryId == SelectedCategory.Id);

            if (SelectedProject != null)
                query = query.Where(t => t.Transaction.ProjectId == SelectedProject.Id);
            // NOWOŚĆ: Filtr konta (używany tylko w widoku globalnym)
            if (SelectedAccount != null)
            {
                query = query.Where(x => x.Transaction.AccountId == SelectedAccount.Id);
            }

            // 4. Sortowanie
            query = _currentSortColumn switch
            {
                "Date" => _isAscending ? query.OrderBy(t => t.Transaction.Date) : query.OrderByDescending(t => t.Transaction.Date),
                "Account" => _isAscending ? query.OrderBy(t => t.AccountName) : query.OrderByDescending(t => t.AccountName),
                "Category" => _isAscending ? query.OrderBy(t => t.CategoryName) : query.OrderByDescending(t => t.CategoryName),
                "Description" => _isAscending ? query.OrderBy(t => t.Description) : query.OrderByDescending(t => t.Description),
                "Project" => _isAscending ? query.OrderBy(t => t.ProjectName) : query.OrderByDescending(t => t.ProjectName),
                "Amount" => _isAscending ? query.OrderBy(t => t.Transaction.Amount) : query.OrderByDescending(t => t.Transaction.Amount),
                
                _ => query
            };

            // 5. Aktualizacja ekranu
            Transactions.Clear();
            foreach (var item in query.ToList())
            {
                Transactions.Add(item);
            }
        }
    }

    // Klasa pomocnicza dla tego widoku (spłaszcza obiekt z bazy)
    public class TransactionDisplayItem
    {
        public Transaction Transaction { get; set; } = null!;
        public string CategoryName { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Color AmountColor => Transaction.Type == TransactionType.Expense ? Color.FromArgb("#E53935") : Color.FromArgb("#4CAF50");

        // NOWOŚĆ: Nazwa konta do wyświetlenia na liście
        public string AccountName { get; set; } = string.Empty;

        // NOWOŚĆ: Flaga decydująca, czy pokazać nazwę konta w danym rzędzie
        public bool ShowAccount { get; set; }
    }
}