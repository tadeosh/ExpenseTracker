using ExpenseTracker.Helpers;

namespace ExpenseTracker.Views;

public partial class SplashPage : ContentPage
{
    public SplashPage()
    {
        InitializeComponent();

        VersionLabel.Text = AppVersionHelper.DisplayVersion;
    }
}
