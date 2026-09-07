using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ExpenseTracker.Helpers;
using ExpenseTracker.Messages;
using ExpenseTracker.Models;
using ExpenseTracker.Resources.Strings;
using ExpenseTracker.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

        [ObservableProperty]
        public partial ObservableCollection<TransactionDisplayItem> Transactions { get; set; } = new();

        private bool _needsReload = true; // Flaga chroniąca przed podwójnym ładowaniem

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
            // Rejestracja do nasłuchiwania zmian
            WeakReferenceMessenger.Default.Register<TransactionsViewModel, TransactionsChangedMessage>(this, (r, m) =>
            {
                // Teraz kompilator wie, że 'r' to TransactionsViewModel, więc ma dostęp do pola
                r._needsReload = true;
            });
        }

        // Odbiór wiadomości z AddTransactionViewModel
        public void Receive(TransactionsChangedMessage message)
        {
            // Nie ładujemy danych od razu! Zaznaczamy tylko, że widok jest "brudny".
            _needsReload = true;
        }

        // Tę metodę powinieneś wywoływać w np. w OnNavigatedTo na stronie TransactionsPage
        public async Task LoadDataIfNeededAsync()
        {
            if (!_needsReload) return;

            // Zabezpieczenie animacji: Czekamy 300ms aż animacja przejścia MAUI (Slide) w 100% się zakończy!
            await Task.Delay(300);

           // await FetchAndApplyFiltersAsync();
           await LoadDataAsync();
            _needsReload = false;
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
            var newCollection = await Task.Run(() =>
            {
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
                    IsTransferIn = dto.IsTransferIn,
                    Currency = dto.AccountCurrency ?? "" // NOWOŚĆ
                }).ToList();
                return new ObservableCollection<TransactionDisplayItem>(displayItems);
            });

            MainThread.BeginInvokeOnMainThread(() =>
            {

                Transactions = newCollection;
            });
        }

        // ========= CRUD ========================
        [RelayCommand]
        private async Task EditTransactionAsync(TransactionDisplayItem item)
        {
            if (item == null || item.Transaction == null)
                return;

            var navParams = new Dictionary<string, object>
            {
                // Przekazujemy ID transakcji jako string, tak jak radzi MAUI Shell Navigation
                { "TransactionId", item.Transaction.Id.ToString() }
            };

            // Nawigacja do formularza w trybie edycji (oczekujemy, że AddTransactionPage potrafi obsłużyć ten parametr)
            await Shell.Current.GoToAsync(RoutesHelper.AddTransactionPage, navParams);
        }

        [RelayCommand]
        private async Task DeleteTransactionAsync(TransactionDisplayItem item)
        {
            if (item == null || item.Transaction == null)
                return;

            // Zabezpieczenie przed przypadkowym usunięciem (Clean UX)
            bool isConfirmed = await Shell.Current.DisplayAlertAsync(
                AppResources.WarningTitle,
                AppResources.DeleteConfirmationText,
                AppResources.YesBtn,
                AppResources.CancelBtn);

            if (!isConfirmed)
                return;

            try
            {
                // 1. Usunięcie z bazy danych przy użyciu obiektu Transaction z interfejsu
                await _databaseService.DeleteTransactionAsync(item.Transaction);

                // 2. Natychmiastowe odświeżenie interfejsu (usunięcie z pamięci ObservableCollection)
                Transactions.Remove(item);

                // OPCJONALNIE: Wywołanie metody aktualizującej podsumowania finansowe na górze strony (jeśli takowe posiadasz)
                // await UpdateSummariesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CRITICAL] Błąd podczas usuwania transakcji: {ex.Message}");
                await Shell.Current.DisplayAlertAsync(AppResources.ErrorTitle, AppResources.ErrorGeneral, AppResources.UnderstoodBtn);
            }
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

        public string Currency { get; set; } = string.Empty;

        // NOWOŚĆ: Precyzyjne sterowanie znakiem kwoty dla interfejsu
        public string AmountDisplay
        {
            get
            {
                string prefix = SignedAmount > 0 ? "+ " : (SignedAmount < 0 ? "- " : "");
                // Używamy Math.Abs by nie dublować minusa
                return $"{prefix}{Math.Abs(SignedAmount):N2} {Currency}"; //z walutą
            }
        }
    }
}