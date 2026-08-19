using ExpenseTracker.Helpers;

namespace ExpenseTracker
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Rejestracja ścieżek dla stron ukrytych w menu
            Routing.RegisterRoute(RoutesHelper.CategoriesPage, typeof(Views.CategoriesPage));
            Routing.RegisterRoute(RoutesHelper.ProjectsPage, typeof(Views.ProjectsPage));
            Routing.RegisterRoute(RoutesHelper.ExchangeRatesPage, typeof(Views.ExchangeRatesPage));
            Routing.RegisterRoute(RoutesHelper.FavoriteCurrenciesPage, typeof(Views.FavoriteCurrenciesPage));
            Routing.RegisterRoute(RoutesHelper.TransactionsPage, typeof(Views.TransactionsPage));
            Routing.RegisterRoute(RoutesHelper.AddTransactionPage, typeof(Views.AddTransactionPage));
        }
    }
}