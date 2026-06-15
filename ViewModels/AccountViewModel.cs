using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using System.Collections.ObjectModel;

namespace ExpenseTracker.ViewModels
{
    public partial class AccountsViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        public ObservableCollection<Account> Accounts { get; } = new();

        // NOWY STANDARD AOT: public partial Typ NazwaZDużejLitery { get; set; }
        [ObservableProperty]
        public partial string AccountName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string AccountCurrency { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string AccountBalance { get; set; } = string.Empty;

        public AccountsViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
          //  LoadAccountsAsync();
        }

        [RelayCommand]
        private async Task AddAccountAsync()
        {
            if (string.IsNullOrWhiteSpace(AccountName) || string.IsNullOrWhiteSpace(AccountCurrency))
                return;

            if (!decimal.TryParse(AccountBalance, out decimal initialBalance))
                return;

            var newAccount = new Account
            {
                Name = AccountName,
                Currency = AccountCurrency,
                InitialBalance = initialBalance
            };

            await _databaseService.SaveAccountAsync(newAccount);
            Accounts.Add(newAccount);

            AccountName = string.Empty;
            AccountCurrency = string.Empty;
            AccountBalance = string.Empty;
        }

        public async Task LoadAccountsAsync()
        {
            var accountsFromDb = await _databaseService.GetAccountsAsync();
            Accounts.Clear();
            foreach (var acc in accountsFromDb)
            {
                Accounts.Add(acc);
            }
        }
    }
}