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
    public partial class AddTransactionViewModel : ObservableObject, IQueryAttributable
    {
        private readonly IDatabaseService _databaseService;

        // ZMIANA: Typy zmienione na int? - koniec z int.TryParse!
        [ObservableProperty]
        public partial int? PreselectedAccountId { get; set; }

        [ObservableProperty]
        public partial int? PreselectedCategoryId { get; set; }

        [ObservableProperty]
        public partial int? PreselectedProjectId { get; set; }
        [ObservableProperty]
        public partial int? TransactionId { get; set; }

        public ObservableCollection<Account> Accounts { get; } = new();
        //public ObservableCollection<Project> Projects { get; } = new();
        public ObservableCollection<ProjectDisplayItem> MainProjects { get; } = new();
        public ObservableCollection<ProjectDisplayItem> SubProjects { get; } = new();
        public ObservableCollection<CategoryDisplayItem> MainCategories { get; } = new();
        public ObservableCollection<CategoryDisplayItem> SubCategories { get; } = new();

        private List<Category> _allCategories = new();
        private List<Project> _allProjects = new();
        private decimal? _lastFetchedRate = null;

        [ObservableProperty]
        public partial bool IsCategoryDropdownOpen { get; set; } = false;

        [ObservableProperty]
        public partial string SelectedCategoryName { get; set; } = AppResources.CategoryLabel ?? "Kategoria";

        [ObservableProperty]
        public partial string SelectedCategoryColorHex { get; set; } = "Transparent";

        [ObservableProperty]
        public partial bool IsProjectDropdownOpen { get; set; } = false;

        [ObservableProperty]
        public partial string SelectedProjectName { get; set; } = AppResources.ProjectLabel ?? "Projekt (Opcjonalnie)";


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
        public partial int SelectedTypeIndex { get; set; } = 0;

        [ObservableProperty]
        public partial Account? DestinationAccount { get; set; }

        [ObservableProperty]
        public partial string ExchangeRateText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsTransfer { get; set; }

        [ObservableProperty]
        public partial bool IsCurrencyConversion { get; set; }

        [ObservableProperty]
        public partial string CurrencyConversionLabel { get; set; } = string.Empty;

        //Do wyswietlania błędów walidacji w UI
        [ObservableProperty] public partial string? AmountError { get; set; }
        [ObservableProperty] public partial string? AccountError { get; set; }
        [ObservableProperty] public partial string? DestinationAccountError { get; set; }
        [ObservableProperty] public partial string? ExchangeRateError { get; set; }

        private readonly IValidator<AddTransactionViewModel> _validator;
        public AddTransactionViewModel(IDatabaseService databaseService, IValidator<AddTransactionViewModel> validator)
        {
            _databaseService = databaseService;
            _validator = validator;
        }

        // Metoda do czyszczenia błędów przed kolejną próbą zapisu
        private void ClearErrors()
        {
            AmountError = AccountError = DestinationAccountError = ExchangeRateError = null;
        }

        // ================ implementacja interfejscu Queryattributable ============================
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            // Ręczne, w pełni bezpieczne rozpakowanie (unboxing) z object do int
            if (query.TryGetValue("PreselectedAccountId", out var accountIdObj) && accountIdObj is int accountId)
            {
                PreselectedAccountId = accountId;
            }

            if (query.TryGetValue("PreselectedCategoryId", out var categoryIdObj) && categoryIdObj is int categoryId)
            {
                PreselectedCategoryId = categoryId;
            }

            if (query.TryGetValue("PreselectedProjectId", out var projectIdObj) && projectIdObj is int projectId)
            {
                PreselectedProjectId = projectId;
            }
            // NOWOŚĆ: Odbiór ID transakcji do edycji
            if (query.TryGetValue("TransactionId", out var transIdObj) && transIdObj is string transIdStr)
            {
                if (int.TryParse(transIdStr, out int transId))
                {
                    TransactionId = transId;
                }
            }
        }
        // ================ koniec implementacji interfejsu ====================================

        partial void OnSelectedTypeIndexChanged(int value)
        {
            IsTransfer = value == 2;
            CheckCurrencyConversion();
        }

        partial void OnSelectedAccountChanged(Account? value) => CheckCurrencyConversion();
        partial void OnDestinationAccountChanged(Account? value) => CheckCurrencyConversion();
        partial void OnSelectedDateChanged(DateTime value) => CheckCurrencyConversion();

        // ZMIANA: Usuwamy "async void" i opakowujemy logikę!
        private void CheckCurrencyConversion()
        {
            // Jeśli warunki nie są spełnione, od razu zerujemy
            if (!IsTransfer || SelectedAccount == null || DestinationAccount == null || SelectedAccount.Currency == DestinationAccount.Currency)
            {
                IsCurrencyConversion = false;
                CurrencyConversionLabel = string.Empty;
                ExchangeRateText = string.Empty;
                _lastFetchedRate = null;
                return;
            }

            IsCurrencyConversion = true;
            CurrencyConversionLabel = $"{SelectedAccount.Currency} -> {DestinationAccount.Currency}";

            // "Fire-and-forget" w tle, bezpieczne dla wątku UI!
            Task.Run(async () =>
            {
                var rate = await _databaseService.GetApplicableExchangeRateAsync(
                    SelectedAccount.Currency,
                    DestinationAccount.Currency,
                    SelectedDate);

                // Kiedy mamy wynik z bazy, wracamy na wątek główny żeby zaktualizować XAML
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (rate.HasValue)
                    {
                        ExchangeRateText = rate.Value.ToString("0.####", CultureInfo.InvariantCulture);
                        _lastFetchedRate = rate.Value;
                    }
                    else
                    {
                        ExchangeRateText = string.Empty;
                        _lastFetchedRate = null;
                    }
                });
            });
        }
        //==============================================================================
        // ================ Ładowanie danych z bazy ====================================
        //==============================================================================
        public async Task LoadDataAsync()
        {
            // Domyślne wartości z pre-selekcji (dla nowej transakcji)
            int? savedAccountId = SelectedAccount?.Id ?? PreselectedAccountId;
            int? savedDestAccountId = DestinationAccount?.Id;
            int? savedProjectId = SelectedProject?.Id ?? PreselectedProjectId;
            int? savedCategoryId = SelectedCategory?.Id ?? PreselectedCategoryId;

            // --- LOGIKA EDYCJI TRANSAKCJI ---
            if (TransactionId.HasValue)
            {
                var existingTx = await _databaseService.GetTransactionAsync(TransactionId.Value);
                if (existingTx != null)
                {
                    // Przepisanie danych tekstowych i numerycznych
                    AmountText = existingTx.Amount.ToString("0.##", CultureInfo.InvariantCulture);
                    SelectedDate = existingTx.Date;
                    DescriptionText = existingTx.Description;
                    SelectedTypeIndex = (int)existingTx.Type;

                    // Przypisanie ID do późniejszego wyciągnięcia pełnych obiektów ze słowników
                    savedAccountId = existingTx.AccountId;
                    savedDestAccountId = existingTx.DestinationAccountId;
                    savedCategoryId = existingTx.CategoryId;
                    savedProjectId = existingTx.ProjectId;

                    if (existingTx.ExchangeRate.HasValue)
                    {
                        ExchangeRateText = existingTx.ExchangeRate.Value.ToString("0.####", CultureInfo.InvariantCulture);
                        _lastFetchedRate = existingTx.ExchangeRate.Value;
                    }
                }
            }

            var accountsFromDb = await _databaseService.GetAccountsAsync();
            Accounts.Clear();
            foreach (var acc in accountsFromDb) Accounts.Add(acc);

            _allProjects = await _databaseService.GetProjectsAsync(includeArchived: false);
            var mainProjs = _allProjects.Where(p => p.ParentId == null).OrderBy(p => p.DisplayOrder).ToList();

            MainProjects.Clear();
            // Dodajemy opcję czyszczenia pola (Id = 0)
            MainProjects.Add(new ProjectDisplayItem { Project = new Project { Id = 0, Name = AppResources.CategoryNone ?? "Brak" } });

            foreach (var proj in mainProjs)
            {
                var subCount = _allProjects.Count(p => p.ParentId == proj.Id);
                MainProjects.Add(new ProjectDisplayItem { Project = proj, SubprojectsCount = subCount });
            }

            _allCategories = await _databaseService.GetCategoriesAsync();
            var mainCats = _allCategories.Where(c => c.ParentId == null).OrderBy(c => c.DisplayOrder).ToList();

            MainCategories.Clear();
            MainCategories.Add(new CategoryDisplayItem { Category = new Category { Id = 0, Name = AppResources.CategoryNone ?? "Brak" } });
            foreach (var cat in mainCats)
            {
                var subCount = _allCategories.Count(c => c.ParentId == cat.Id);
                MainCategories.Add(new CategoryDisplayItem { Category = cat, SubcategoriesCount = subCount });
            }

            // ODTWARZANIE WYBORÓW (Uproszczone dzięki auto-parsowaniu QueryProperty)
            if (savedAccountId.HasValue) SelectedAccount = Accounts.FirstOrDefault(a => a.Id == savedAccountId.Value);
            else if (PreselectedAccountId.HasValue) SelectedAccount = Accounts.FirstOrDefault(a => a.Id == PreselectedAccountId.Value);

            if (savedProjectId.HasValue) SelectedProject = _allProjects.FirstOrDefault(p => p.Id == savedProjectId.Value);
            else if (PreselectedProjectId.HasValue) SelectedProject = _allProjects.FirstOrDefault(p => p.Id == PreselectedProjectId.Value);

            if (savedCategoryId.HasValue) SelectedCategory = _allCategories.FirstOrDefault(c => c.Id == savedCategoryId.Value);
            else if (PreselectedCategoryId.HasValue) SelectedCategory = _allCategories.FirstOrDefault(c => c.Id == PreselectedCategoryId.Value);

            if (savedDestAccountId.HasValue) DestinationAccount = Accounts.FirstOrDefault(a => a.Id == savedDestAccountId.Value);

            if (SelectedCategory != null)
            {
                SelectedCategoryName = SelectedCategory.Name;
                SelectedCategoryColorHex = SelectedCategory.ColorHex;
            }
        }
        //==============================================================================
        // ================== Koniec ładowania =========================================
        //==============================================================================

        [RelayCommand]
        private void ToggleCategoryDropdown()
        {
            IsCategoryDropdownOpen = !IsCategoryDropdownOpen;
            if (!IsCategoryDropdownOpen)
            {
                SubCategories.Clear();
            }
        }

        // ZMIANA: Zastępujemy refleksję komendami!
        [RelayCommand]
        private void MainCategoryTapped(CategoryDisplayItem? item)
        {
            if (item == null) return;

            SubCategories.Clear();
            var subs = _allCategories.Where(c => c.ParentId == item.Category.Id).OrderBy(c => c.DisplayOrder).ToList();

            if (subs.Any())
            {
                SubCategories.Add(new CategoryDisplayItem { Category = item.Category });
                foreach (var sub in subs) SubCategories.Add(new CategoryDisplayItem { Category = sub });
            }
            else
            {
                ConfirmCategorySelection(item.Category);
            }
        }

        [RelayCommand]
        private void SubCategoryTapped(CategoryDisplayItem? item)
        {
            if (item == null) return;
            ConfirmCategorySelection(item.Category);
        }

        private void ConfirmCategorySelection(Category category)
        {
            SelectedCategory = category;
            SelectedCategoryName = category.Name;
            SelectedCategoryColorHex = category.ColorHex;
            IsCategoryDropdownOpen = false;
            SubCategories.Clear();
        }

        // to samo dla projektów
        [RelayCommand]
        private void ToggleProjectDropdown()
        {
            IsProjectDropdownOpen = !IsProjectDropdownOpen;
            if (!IsProjectDropdownOpen)
            {
                SubProjects.Clear();
            }
        }

        [RelayCommand]
        private void MainProjectTapped(ProjectDisplayItem? item)
        {
            if (item == null) return;

            if (item.Project.Id == 0)
            {
                SelectedProject = null;
                SelectedProjectName = AppResources.ProjectLabel ?? "Projekt (Opcjonalnie)";
                IsProjectDropdownOpen = false;
                return;
            }

            SubProjects.Clear();
            var subs = _allProjects.Where(p => p.ParentId == item.Project.Id).OrderBy(p => p.DisplayOrder).ToList();

            if (subs.Any())
            {
                SubProjects.Add(new ProjectDisplayItem { Project = item.Project });
                foreach (var sub in subs) SubProjects.Add(new ProjectDisplayItem { Project = sub });
            }
            else
            {
                ConfirmProjectSelection(item.Project);
            }
        }

        [RelayCommand]
        private void SubProjectTapped(ProjectDisplayItem? item)
        {
            if (item == null) return;
            ConfirmProjectSelection(item.Project);
        }

        private void ConfirmProjectSelection(Project project)
        {
            SelectedProject = project;
            SelectedProjectName = project.Name;
            IsProjectDropdownOpen = false;
            SubProjects.Clear();
        }
        // end prjekty

        [RelayCommand]
        private async Task SaveTransactionAsync()
        {
            ClearErrors();

            // Uruchomienie FluentValidation
            var validationResult = await _validator.ValidateAsync(this);

            if (!validationResult.IsValid)
            {
                // Mapowanie błędów do właściwości UI
                foreach (var error in validationResult.Errors)
                {
                    switch (error.PropertyName)
                    {
                        case nameof(AmountText): AmountError = error.ErrorMessage; break;
                        case nameof(SelectedAccount): AccountError = error.ErrorMessage; break;
                        case nameof(DestinationAccount): DestinationAccountError = error.ErrorMessage; break;
                        case nameof(ExchangeRateText): ExchangeRateError = error.ErrorMessage; break;
                    }
                }
                return; // Zatrzymujemy zapis, błędy pojawią się w UI
            }

            // --- ZAPIS ---
            // Skoro walidacja przeszła (IsVaild = true), parsujemy z pewnością sukcesu
            decimal amount = decimal.Parse(AmountText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture);

            TransactionType type = SelectedTypeIndex switch
            {
                1 => TransactionType.Income,
                2 => TransactionType.Transfer,
                _ => TransactionType.Expense
            };

            decimal? exchangeRate = null;
            if (type == TransactionType.Transfer && IsCurrencyConversion)
            {
                decimal parsedRate = decimal.Parse(ExchangeRateText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture);
                exchangeRate = parsedRate;

                if (_lastFetchedRate == null || _lastFetchedRate.Value != parsedRate)
                {
                    var newLearnedRate = new ExchangeRate
                    {
                        SourceCurrency = SelectedAccount!.Currency,
                        TargetCurrency = DestinationAccount!.Currency,
                        Rate = parsedRate,
                        Date = SelectedDate
                    };
                    await _databaseService.SaveExchangeRateAsync(newLearnedRate);
                }
            }

            var transaction = new Transaction
            {
                Id = TransactionId ?? 0, // Jeśli null, wstaw 0 (INSERT). Jeśli ma wartość, zaktualizuje rekord (UPDATE)
                Amount = amount,
                Date = SelectedDate,
                Description = DescriptionText,
                Type = type,
                AccountId = SelectedAccount!.Id,
                CategoryId = SelectedCategory?.Id,
                ProjectId = SelectedProject?.Id,
                DestinationAccountId = type == TransactionType.Transfer ? DestinationAccount?.Id : null,
                ExchangeRate = exchangeRate
            };

            await _databaseService.SaveTransactionAsync(transaction);
            await CloseFormSafeAsync();
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            ClearForm();
            await CloseFormSafeAsync();
        }

        private async Task CloseFormSafeAsync()
        {
            if (Shell.Current.Navigation.NavigationStack.Count > 1)
                await Shell.Current.Navigation.PopAsync();
            else
                await Shell.Current.GoToAsync("//HomePage");
        }

        private void ClearForm()
        {
            ClearErrors();
            AmountText = string.Empty;
            DescriptionText = string.Empty;
            SelectedAccount = null;
            DestinationAccount = null;
            SelectedCategory = null;
            SelectedCategoryName = AppResources.CategoryLabel ?? "Kategoria";
            SelectedCategoryColorHex = "Transparent";
            SelectedProject = null;
            ExchangeRateText = string.Empty;

            PreselectedAccountId = null;
            PreselectedCategoryId = null;
            PreselectedProjectId = null;
            TransactionId = null;
        }

        [RelayCommand]
        private void SetTransactionType(string typeIndex)
        {
            if (int.TryParse(typeIndex, out int index)) SelectedTypeIndex = index;
        }

        [RelayCommand]
        private async Task GoToCategoriesAsync() => await Shell.Current.GoToAsync("CategoriesPage");

        [RelayCommand]
        private async Task GoToProjectsAsync() => await Shell.Current.GoToAsync("ProjectsPage");
    }
}