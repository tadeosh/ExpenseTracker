using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using System.Collections.ObjectModel;
using ExpenseTracker.Resources.Strings;

namespace ExpenseTracker.ViewModels
{
    // Odbieramy parametr AccountId z nawigacji
    [QueryProperty(nameof(AccountId), "AccountId")]
    public partial class TransactionsViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        // Odbieramy parametr AccountId z nawigacji
        [ObservableProperty]
        public partial int AccountId { get; set; }

        // Przechowuje nazwę konta do wyświetlenia w nagłówku
        [ObservableProperty]
        public partial string AccountName { get; set; } = "Ładowanie...";

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

        public TransactionsViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        // Ta metoda odpali się automatycznie, gdy nadejdzie AccountId z HomePage
        partial void OnAccountIdChanged(int value)
        {
            // Zabezpieczenie: Wymuszamy, by całe ładowanie i przypisywanie do ObservableCollection
            // działo się na głównym wątku interfejsu użytkownika (MainThread).
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await LoadDataAsync();
            });
        }

        public async Task LoadDataAsync()
        {
            try
            {
                // 1. Pobieramy wszystkie potrzebne dane z bazy
                var accounts = await _databaseService.GetAccountsAsync();
                var categories = await _databaseService.GetCategoriesAsync();
                var projects = await _databaseService.GetProjectsAsync();
                var rawTransactions = await _databaseService.GetTransactionsAsync();

                // 2a. NOWOŚĆ: Szukamy nazwy konta i ustawiamy ją do wyświetlenia w XAML
                var currentAccount = accounts.FirstOrDefault(a => a.Id == AccountId);
                if (currentAccount != null)
                {
                    // Sklejamy przetłumaczony tytuł (np. "Transakcje konta") z nazwą wybranego konta
                    AccountName = $"{AppResources.TransactionsPageTitle}: {currentAccount.Name}";
                }

                // 2b. ZAPAMIĘTUJEMY AKTUALNE FILTRY
                int? savedCategoryId = SelectedCategory?.Id;
                int? savedProjectId = SelectedProject?.Id;

                // 3a. Ładujemy pickery
                AvailableCategories.Clear();
                AvailableProjects.Clear();
                foreach (var c in categories) AvailableCategories.Add(c);
                foreach (var p in projects) AvailableProjects.Add(p);

                // 3b. ODTWARZAMY FILTRY PO ZAŁADOWANIU
                if (savedCategoryId.HasValue)
                    SelectedCategory = AvailableCategories.FirstOrDefault(c => c.Id == savedCategoryId.Value);
                if (savedProjectId.HasValue)
                    SelectedProject = AvailableProjects.FirstOrDefault(p => p.Id == savedProjectId.Value);

                // 4. Budujemy transakcje
                _allTransactions = rawTransactions
                    .Where(t => t.AccountId == AccountId)
                    .Select(t => new TransactionDisplayItem
                    {
                        Transaction = t,
                        CategoryName = categories.FirstOrDefault(c => c.Id == t.CategoryId)?.Name ?? "-",
                        ProjectName = projects.FirstOrDefault(p => p.Id == t.ProjectId)?.Name ?? "-",
                        Description = t.Description ?? string.Empty
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
        }

        [RelayCommand]
        private async Task NavigateToAddTransactionAsync()
        {
            // Budujemy bazowy adres z kontem
            string url = $"AddTransactionPage?PreselectedAccountId={AccountId}";

            // Jeśli kategoria jest przefiltrowana, doklejamy jej ID
            if (SelectedCategory != null)
                url += $"&PreselectedCategoryId={SelectedCategory.Id}";

            // Jeśli projekt jest przefiltrowany, doklejamy jego ID
            if (SelectedProject != null)
                url += $"&PreselectedProjectId={SelectedProject.Id}";

            await Shell.Current.GoToAsync(url);
           // await Shell.Current.GoToAsync($"AddTransactionPage?PreselectedAccountId={AccountId}");
        }

        private void UpdateSortIcons()
        {
            // Resetujemy wszystkie ikonki
            DateSortIcon = CategorySortIcon = DescriptionSortIcon = ProjectSortIcon = AmountSortIcon = "";
            string icon = _isAscending ? "▲" : "▼";

            switch (_currentSortColumn)
            {
                case "Date": DateSortIcon = icon; break;
                case "Category": CategorySortIcon = icon; break;
                case "Description": DescriptionSortIcon = icon; break;
                case "Project": ProjectSortIcon = icon; break;
                case "Amount": AmountSortIcon = icon; break;
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

            // 4. Sortowanie
            query = _currentSortColumn switch
            {
                "Date" => _isAscending ? query.OrderBy(t => t.Transaction.Date) : query.OrderByDescending(t => t.Transaction.Date),
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
    }
}