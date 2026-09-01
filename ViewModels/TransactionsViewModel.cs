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

                // ZMIANA: Usunięto generowanie słowników! 
                // Pobieramy nazwę konta bezpośrednio z pobranej listy za pomocą LINQ
                if (_parsedAccountId.HasValue)
                {
                    var account = accounts.FirstOrDefault(a => a.Id == _parsedAccountId.Value);
                    AccountName = account != null
                        ? $"{AppResources.TransactionsPageTitle}: {account.Name}"
                        : AppResources.AllTransactionsTitle ?? "Wszystkie transakcje";
                }
                else
                {
                    AccountName = AppResources.AllTransactionsTitle ?? "Wszystkie transakcje";
                }

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

        // NOWOŚĆ: Token do zarządzania cyklem życia asynchronicznych opóźnień
        private CancellationTokenSource? _debounceCts;

        // 1. Pola tekstowe - wywołują opóźniony (Debounced) filtr
        partial void OnSearchTextChanged(string value) => DebounceFilterUpdate();
        partial void OnMinAmountTextChanged(string value) => DebounceFilterUpdate();
        partial void OnMaxAmountTextChanged(string value) => DebounceFilterUpdate();

        // 2. Kontrolki wyboru (Pickery/Sortowanie) - wywołują filtr natychmiast (lepszy UX)
        partial void OnSelectedCategoryChanged(Category? value) => FireFilterUpdate();
        partial void OnSelectedProjectChanged(Project? value) => FireFilterUpdate();
        partial void OnSelectedAccountChanged(Account? value) => FireFilterUpdate();

        // NOWOŚĆ: Wzorzec Debounce dla wpisywania tekstu
        private async void DebounceFilterUpdate()
        {
            try
            {
                // Anuluj poprzednie odliczanie, jeśli użytkownik wpisał kolejny znak przed upływem 300ms
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();

                // Utwórz nowy token dla bieżącego znaku
                _debounceCts = new CancellationTokenSource();
                var token = _debounceCts.Token;

                // Czekamy 300ms (optymalny czas na zrobienie pauzy podczas pisania)
                await Task.Delay(300, token);

                // Jeśli po 300ms token nie został anulowany (użytkownik przestał pisać), odpalamy bazę
                if (!token.IsCancellationRequested)
                {
                    await FetchAndApplyFiltersAsync();
                }
            }
            catch (TaskCanceledException)
            {
                // Całkowicie ignorujemy ten wyjątek - to naturalne zachowanie przy anulowaniu Task.Delay
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd podczas debouncingu: {ex.Message}");
            }
        }

        private async void FireFilterUpdate()
        {
            try
            {
                // Jeśli wymuszamy natychmiastowe odświeżenie (np. kliknięcie w picker),
                // anulujemy też ewentualne wiszące zapytania tekstowe.
                _debounceCts?.Cancel();

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
            decimal? min = decimal.TryParse(MinAmountText?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pMin) ? pMin : null;
            decimal? max = decimal.TryParse(MaxAmountText?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pMax) ? pMax : null;

            int? targetAccountId = SelectedAccount?.Id ?? _parsedAccountId;

            // Cały ciężar operacji (Sortowanie, JOIN, Filtrowanie tekstu) przejmuje SQLite!
            var rawData = await _databaseService.GetTransactionsWithDetailsAsync(
                targetAccountId,
                SelectedCategory?.Id,
                SelectedProject?.Id,
                min,
                max,
                SearchText,
                _currentSortColumn,
                _isAscending);

            // Błyskawiczne mapowanie z DTO do modelu widoku
            var displayItems = rawData.Select(dto => new TransactionDisplayItem
            {
                Transaction = new Transaction
                {
                    Id = dto.Id,
                    Amount = dto.Amount,
                    Date = dto.Date,
                    Type = (TransactionType)dto.Type,
                    AccountId = dto.AccountId
                },
                CategoryName = dto.CategoryName,
                ProjectName = dto.ProjectName,
                AccountName = dto.AccountName,
                Description = dto.Description,
                ShowAccount = IsGlobalView,
                SignedAmount = dto.SignedAmount,
                IsTransferIn = dto.IsTransferIn
            }).ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Transactions.Clear();
                foreach (var item in displayItems)
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
        //public Color AmountColor => Transaction.Type == TransactionType.Expense ? Color.FromArgb("#E53935") : Color.FromArgb("#4CAF50");
        public string AccountName { get; set; } = string.Empty;
        public bool ShowAccount { get; set; }
        // NOWOŚĆ: Logiczna wartość ujemna/dodatnia na potrzeby prawidłowego sortowania
        //public decimal SignedAmount => Transaction.Type == TransactionType.Expense ? -Transaction.Amount : Transaction.Amount;
        // Zasilane bezpośrednio z bazy
        public decimal SignedAmount { get; set; }
        public bool IsTransferIn { get; set; }

        public Color AmountColor => Transaction.Type switch
        {
            TransactionType.Expense => Color.FromArgb("#E53935"),
            TransactionType.Income => Color.FromArgb("#4CAF50"),
            TransactionType.Transfer => Color.FromArgb("#1E88E5"), // Niebieski dla wszystkich transferów
            _ => Colors.Gray
        };

        // NOWOŚĆ: Precyzyjne sterowanie znakiem kwoty dla interfejsu
        public string AmountDisplay
        {
            get
            {
                string prefix = SignedAmount > 0 ? "+ " : (SignedAmount < 0 ? "- " : "");
                // Używamy Math.Abs by nie dublować minusa
                return $"{prefix}{Math.Abs(SignedAmount):N2}";
            }
        }
    }
}