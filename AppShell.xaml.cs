namespace ExpenseTracker
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Rejestracja ścieżek dla stron ukrytych w menu
            Routing.RegisterRoute("CategoriesPage", typeof(Views.CategoriesPage));
            Routing.RegisterRoute("ProjectsPage", typeof(Views.ProjectsPage));
            Routing.RegisterRoute("ExchangeRatesPage", typeof(Views.ExchangeRatesPage));
            Routing.RegisterRoute("FavoriteCurrenciesPage", typeof(Views.FavoriteCurrenciesPage));
        }
    }
}