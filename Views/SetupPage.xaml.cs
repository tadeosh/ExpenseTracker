using ExpenseTracker.Resources.Strings;
using Microsoft.Maui.Storage;

namespace ExpenseTracker.Views
{
    public partial class SetupPage : ContentPage
    {
        public SetupPage()
        {
            InitializeComponent();
        }

        // Metoda odpala się natychmiast, gdy ekran staje się widoczny
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Sprawdzamy w schowku, czy hasło już tam jest
            var existingPassword = await SecureStorage.Default.GetAsync("DbPassword");

            if (!string.IsNullOrEmpty(existingPassword))
            {
                // MAGIA: Jeśli mamy hasło, bez mrugnięcia okiem podmieniamy aplikację na główną!
                //Application.Current.MainPage = new AppShell();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (Application.Current?.Windows.Count > 0)
                    {
                        Application.Current.Windows[0].Page = new AppShell();
                    }
                });
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            ErrorLabel.IsVisible = false;

            if (string.IsNullOrWhiteSpace(PasswordEntry.Text) || string.IsNullOrWhiteSpace(ConfirmPasswordEntry.Text))
            {
                ErrorLabel.Text = AppResources.ErrorPasswordEmpty;
                ErrorLabel.IsVisible = true;
                return;
            }

            if (PasswordEntry.Text != ConfirmPasswordEntry.Text)
            {
                ErrorLabel.Text = AppResources.ErrorPasswordMismatch;
                ErrorLabel.IsVisible = true;
                return;
            }

            // 1. Zapisujemy hasło bezpiecznie w sprzętowym schowku telefonu
            await SecureStorage.Default.SetAsync("DbPassword", PasswordEntry.Text);

            // 2. MAGIA: Podmieniamy aplikację na główną, która teraz bez problemu połączy się z bazą!
            //Application.Current.MainPage = new AppShell();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Application.Current?.Windows.Count > 0)
                {
                    Application.Current.Windows[0].Page = new AppShell();
                }
            });
        }
    }
}