using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Resources.Strings;
using System.Collections.ObjectModel;
using ExpenseTracker.Services.Interfaces;
using System.Globalization;

namespace ExpenseTracker.ViewModels
{
    [QueryProperty(nameof(AccountIdParam), "AccountId")]
    public partial class TransactionsViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;

        [ObservableProperty]
        public partial string? AccountIdParam { get; set; }

        private int? _parsedAccountId;

        [ObservableProperty]
        public partial string AccountName { get; set; } = "...";

        public ObservableCollection<TransactionDisplayItem> Transactions { get; } = new();

        [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
        [ObservableProperty] public partial string MinAmountText { get; set; } = string.Empty;
        [ObservableProperty] public partial string MaxAmountText { get; set; } = string.Empty;

        public ObservableCollection<Category> AvailableCategories { get; } = new();
        public ObservableCollection<Project> AvailableProjects { get; } = new();
        public ObservableCollection<Account> AvailableAccounts { get; } = new();

        [ObservableProperty] public partial Category? SelectedCategory { get; set; }
        [ObservableProperty] public partial Project? SelectedProject { get; set; }
        [ObservableProperty] public partial Account? SelectedAccount { get; set; }

        private string _currentSortColumn = "Date";
        private bool _isAscending = false;

        [ObservableProperty] public partial string DateSortIcon { get; set; } = "▼";
        [ObservableProperty] public partial string CategorySortIcon { get; set; } = "";
        [ObservableProperty] public partial string DescriptionSortIcon { get; set; } = "";
        [ObservableProperty] public partial string ProjectSortIcon { get; set; } = "";
        [ObservableProperty] public partial string AmountSortIcon { get; set; } = "";
        [ObservableProperty] public partial string AccountSortIcon { get; set; } = "";

        [ObservableProperty]
        public partial bool IsGlobalView { get; set; }

        // CACHE DLA SŁOWNIKÓW (Ogromny zysk wydajnościowy)
        private Dictionary<int, string> _categoryDict = new();
        private Dictionary<int, string> _projectDict = new();
        private Dictionary<int, string> _accountDict = new();

        public TransactionsViewModel(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task LoadDataAsync()
        {
            try
            {
                _parsedAccountId = null;
                if (!string.IsNullOrEmpty(AccountIdParam) && int.TryParse(AccountIdParam, out int id))
                {
                    _parsedAccountId = id;
                }

                IsGlobalView = !_parsedAccountId.HasValue;

                // 1. Ładowanie bazowych list słownikowych TYLKO RAZ
                var accounts = await _databaseService.GetAccountsAsync();
                var categories = await _databaseService.GetCategoriesAsync(includeArchived: true);
                var projects = await _databaseService.GetProjectsAsync(includeArchived: true);

                _categoryDict = categories.ToDictionary(x => x.Id, x => x.Name);
                _projectDict = projects.ToDictionary(x => x.Id, x => x.Name);
                _accountDict = accounts.ToDictionary(x => x.Id, x => x.Name);

                if (_parsedAccountId.HasValue && _accountDict.TryGetValue(_parsedAccountId.Value, out var accName))
                    AccountName = $"{AppResources.TransactionsPageTitle}: {accName}";
                else
                    AccountName = AppResources.AllTransactionsTitle ?? "Wszystkie transakcje";

                AvailableCategories.Clear();
                AvailableProjects.Clear();
                AvailableAccounts.Clear();

                foreach (var c in categories) AvailableCategories.Add(c);
                foreach (var p in projects) AvailableProjects.Add(p);
                foreach (var a in accounts) AvailableAccounts.Add(a);

                // 2. Pierwsze załadowanie z bazy
                await FetchAndApplyFiltersAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd podczas ładowania danych: {ex.Message}");
            }
        }

        // --- NASŁUCHIWACZE ZMIAN ---
        // Bezpieczny wzorzec async void używany w odpowiedzi na zdarzenia properties
        partial void OnSearchTextChanged(string value) => FireFilterUpdate();
        partial void OnMinAmountTextChanged(string value) => FireFilterUpdate();
        partial void OnMaxAmountTextChanged(string value) => FireFilterUpdate();
        partial void OnSelectedCategoryChanged(Category? value) => FireFilterUpdate();
        partial void OnSelectedProjectChanged(Project? value) => FireFilterUpdate();
        partial void OnSelectedAccountChanged(Account? value) => FireFilterUpdate();

        private async void FireFilterUpdate()
        {
            try
            {
                await FetchAndApplyFiltersAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd odświeżania: {ex.Message}");
            }
        }

        [RelayCommand]
        private void SortBy(string column)
        {
            if (_currentSortColumn == column)
                _isAscending = !_isAscending;
            else
            {
                _currentSortColumn = column;
                _isAscending = true;
            }

            UpdateSortIcons();
            FireFilterUpdate();
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
            // Pamiętaj, że wyczyszczenie tych właściwości automatycznie wywoła FireFilterUpdate()
        }

        [RelayCommand]
        private async Task NavigateToAddTransactionAsync()
        {
            var navParams = new Dictionary<string, object>();

            if (_parsedAccountId.HasValue) navParams.Add("PreselectedAccountId", _parsedAccountId.Value);
            if (SelectedCategory != null) navParams.Add("PreselectedCategoryId", SelectedCategory.Id);
            if (SelectedProject != null) navParams.Add("PreselectedProjectId", SelectedProject.Id);

            await Shell.Current.GoToAsync("AddTransactionPage", navParams);
        }

        private void UpdateSortIcons()
        {
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

        // --- GŁÓWNY SILNIK DANYCH ---
        private async Task FetchAndApplyFiltersAsync()
        {
            // 1. Parsujemy liczby
            decimal? min = null;
            if (decimal.TryParse(MinAmountText?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsedMin))
                min = parsedMin;

            decimal? max = null;
            if (decimal.TryParse(MaxAmountText?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsedMax))
                max = parsedMax;

            // Wyznaczamy ID konta (lokalne z Pickera lub nadrzędne z URL)
            int? targetAccountId = SelectedAccount?.Id ?? _parsedAccountId;

            // 2. ODDECH DLA RAMU: Pobieramy tylko to, co spełnia sztywne warunki
            var rawFilteredDbData = await _databaseService.GetFilteredTransactionsAsync(
                targetAccountId,
                SelectedCategory?.Id,
                SelectedProject?.Id,
                min,
                max);

            // 3. Mapujemy obiekty i wyszukujemy tekstem w pamięci RAM
            var query = rawFilteredDbData.Select(t => new TransactionDisplayItem
            {
                Transaction = t,
                CategoryName = _categoryDict.TryGetValue(t.CategoryId ?? 0, out var catName) ? catName : "-",
                // NAPRAWIONY BŁĄD Z PROJECT DICT:
                ProjectName = _projectDict.TryGetValue(t.ProjectId ?? 0, out var projName) ? projName : "-",
                Description = t.Description ?? string.Empty,
                AccountName = _accountDict.TryGetValue(t.AccountId, out var accName) ? accName : "-",
                ShowAccount = IsGlobalView
            }).AsEnumerable();

            // Szybkie wyszukiwanie tekstowe
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lowerSearch = SearchText.ToLower();
                query = query.Where(t =>
                    t.Description.ToLower().Contains(lowerSearch) ||
                    t.CategoryName.ToLower().Contains(lowerSearch) ||
                    t.ProjectName.ToLower().Contains(lowerSearch));
            }

            // 4. Sortowanie w pamięci RAM
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

            var finalResults = query.ToList();

            // 5. Bezpieczna aktualizacja interfejsu w głównym wątku (MainThread)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Transactions.Clear();
                foreach (var item in finalResults)
                {
                    Transactions.Add(item);
                }
            });
        }
    }

    public class TransactionDisplayItem
    {
        public Transaction Transaction { get; set; } = null!;
        public string CategoryName { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Color AmountColor => Transaction.Type == TransactionType.Expense ? Color.FromArgb("#E53935") : Color.FromArgb("#4CAF50");
        public string AccountName { get; set; } = string.Empty;
        public bool ShowAccount { get; set; }
    }
}