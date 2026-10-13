using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Helpers;
using ExpenseTracker.Models;
using ExpenseTracker.Resources.Strings;
using ExpenseTracker.Services.Interfaces;
using FluentValidation;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ExpenseTracker.ViewModels
{
    public partial class AccountsViewModel : ObservableObject
    {
        private readonly IDatabaseService _databaseService;
        private readonly IValidator<AccountsViewModel> _validator;
        private readonly ISettingsService _settingsService;

        private Account? _editedAccount;
        private Account? _pendingAccount;
        private DateTime? _earliestTransactionDate;

        public ObservableCollection<Account> Accounts { get; } = new();

        [ObservableProperty]
        public partial string AccountName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string AccountCurrency { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string AccountBalance { get; set; } = string.Empty;

        [ObservableProperty]
        public partial DateTime? InitialBalanceDate { get; set; } = DateTime.Today;

        [ObservableProperty]
        public partial string? AccountNameError { get; set; }

        [ObservableProperty]
        public partial string? AccountCurrencyError { get; set; }

        [ObservableProperty]
        public partial string? AccountBalanceError { get; set; }

        [ObservableProperty]
        public partial string? InitialBalanceDateError { get; set; }

        [ObservableProperty]
        public partial bool IsEditing { get; set; }

        [ObservableProperty]
        public partial bool IsHistoryWarningVisible { get; set; }

        [ObservableProperty]
        public partial string HistoryWarningMessage { get; set; } = string.Empty;

        public string SaveButtonText => IsEditing
            ? AppResources.SaveAccountBtn
            : AppResources.AddAccountBtn;

        public AccountsViewModel(IDatabaseService databaseService, IValidator<AccountsViewModel> validator, ISettingsService settingsService)
        {
            _databaseService = databaseService;
            _validator = validator;
            _settingsService = settingsService;

            ResetForm();
        }

        partial void OnIsEditingChanged(bool value)
        {
            OnPropertyChanged(nameof(SaveButtonText));
        }

        private void ClearErrors()
        {
            AccountNameError = null;
            AccountCurrencyError = null;
            AccountBalanceError = null;
            InitialBalanceDateError = null;
        }

        private void ClearHistoryWarning()
        {
            _pendingAccount = null;
            _earliestTransactionDate = null;
            IsHistoryWarningVisible = false;
            HistoryWarningMessage = string.Empty;
        }

        private void ResetForm()
        {
            _editedAccount = null;
            IsEditing = false;

            AccountName = string.Empty;
            AccountBalance = string.Empty;
            InitialBalanceDate = DateTime.Today;

            var defaultCode = _settingsService.DefaultCurrency;
            AccountCurrency = CurrencyHelper.FormatDisplay(defaultCode);

            ClearErrors();
            ClearHistoryWarning();
        }

        [RelayCommand]
        private async Task AddAccountAsync()
        {
            ClearErrors();
            ClearHistoryWarning();

            var validationResult = await _validator.ValidateAsync(this);

            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    switch (error.PropertyName)
                    {
                        case nameof(AccountName):
                            AccountNameError = error.ErrorMessage;
                            break;

                        case nameof(AccountCurrency):
                            AccountCurrencyError = error.ErrorMessage;
                            break;

                        case nameof(AccountBalance):
                            AccountBalanceError = error.ErrorMessage;
                            break;

                        case nameof(InitialBalanceDate):
                            InitialBalanceDateError = error.ErrorMessage;
                            break;
                    }
                }

                return;
            }

            var account = CreateAccountFromForm();

            if (account.Id != 0 && account.InitialBalanceDate.HasValue)
            {
                var earliestDate = await _databaseService.GetEarliestAccountTransactionDateAsync(account.Id);

                if (earliestDate.HasValue && earliestDate.Value.Date < account.InitialBalanceDate.Value.Date)
                {
                    _pendingAccount = account;
                    _earliestTransactionDate = earliestDate.Value.Date;

                    HistoryWarningMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        AppResources.AccountHistoryWarningMessage,
                        earliestDate.Value.Date);

                    IsHistoryWarningVisible = true;
                    return;
                }
            }

            await SaveAccountInternalAsync(account);
        }

        [RelayCommand]
        private void EditAccount(Account? account)
        {
            if (account is null)
                return;

            _editedAccount = account;
            IsEditing = true;

            AccountName = account.Name;
            AccountCurrency = CurrencyHelper.FormatDisplay(account.Currency);
            AccountBalance = account.InitialBalance.ToString("0.##", CultureInfo.CurrentCulture);
            InitialBalanceDate = account.InitialBalanceDate;

            ClearErrors();
            ClearHistoryWarning();
        }

        [RelayCommand]
        private void CancelEdit()
        {
            ResetForm();
        }

        [RelayCommand]
        private void UseEarliestTransactionDate()
        {
            if (!_earliestTransactionDate.HasValue)
                return;

            InitialBalanceDate = _earliestTransactionDate.Value.Date;
            ClearHistoryWarning();
        }

        [RelayCommand]
        private async Task SaveDespiteHistoryWarningAsync()
        {
            if (_pendingAccount is null)
                return;

            var account = _pendingAccount;

            ClearHistoryWarning();

            await SaveAccountInternalAsync(account);
        }

        partial void OnInitialBalanceDateChanged(DateTime? value)
        {
            if (value.HasValue)
                InitialBalanceDateError = null;

            ClearHistoryWarning();
        }

        private Account CreateAccountFromForm()
        {
            var currencyCode = CurrencyHelper.ExtractCode(AccountCurrency) ?? string.Empty;

            var initialBalance = decimal.Parse(
                AccountBalance.Replace(',', '.'),
                NumberStyles.Any,
                CultureInfo.InvariantCulture);

            return new Account
            {
                Id = _editedAccount?.Id ?? 0,
                Name = AccountName.Trim(),
                Currency = currencyCode,
                InitialBalance = initialBalance,
                InitialBalanceDate = InitialBalanceDate?.Date
            };
        }

        private async Task SaveAccountInternalAsync(Account account)
        {
            await _databaseService.SaveAccountAsync(account);

            if (account.Id == 0)
            {
                // SQLite-net powinno po InsertAsync przypisać wygenerowane Id do obiektu.
                Accounts.Add(account);
            }
            else
            {
                var index = -1;

                for (var i = 0; i < Accounts.Count; i++)
                {
                    if (Accounts[i].Id == account.Id)
                    {
                        index = i;
                        break;
                    }
                }

                if (index >= 0)
                    Accounts[index] = account;
                else
                    Accounts.Add(account);
            }

            ResetForm();
        }

        public async Task LoadAccountsAsync()
        {
            var accountsFromDb = await _databaseService.GetAccountsAsync();

            Accounts.Clear();

            foreach (var account in accountsFromDb)
                Accounts.Add(account);
        }
    }
}
