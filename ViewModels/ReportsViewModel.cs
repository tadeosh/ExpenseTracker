using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Models.Reports;
using ExpenseTracker.Resources.Strings;
using ExpenseTracker.Services.Interfaces;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ExpenseTracker.ViewModels
{
    public enum ReportScope
    {
        Monthly,        
        Last3Months,
        Last6Months,
        Yearly,
        Custom
    }

    public enum ReportTab
    {
        Categories,
        Projects,
        Trend,
        Wealth,
        Top
    }

    public class ReportScopeItem
    {
        public ReportScope Scope { get; set; }
        public string DisplayName { get; set; } = string.Empty;

        public override bool Equals(object? obj) => obj is ReportScopeItem other && Scope == other.Scope;
        public override int GetHashCode() => Scope.GetHashCode();
    }
    //===================================================================================================
    //===================================================================================================
    public partial class ReportsViewModel : ObservableObject
    {
        private readonly IReportService _reportService;

        // Baza do obliczeń (punkt w czasie, po którym przesuwają się strzałki)
        private DateTime _referenceDate = DateTime.Today;

        [ObservableProperty]
        public partial DateTime StartDate { get; set; }

        [ObservableProperty]
        public partial DateTime EndDate { get; set; }

        [ObservableProperty]
        public partial string CurrentPeriodLabel { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasData { get; set; } = true;

        // Kontroluje widoczność strzałek (ukrywamy je dla dynamicznych zakresów typu "Ostatnie 3 miesiące")
        [ObservableProperty]
        public partial bool IsNavigationVisible { get; set; } = true;

        [ObservableProperty]
        public partial ObservableCollection<ISeries> CategorySeries { get; set; } = new();

        // Opcje do Pickera
        [ObservableProperty]
        public partial bool IsCustomDateVisible { get; set; }
        public ObservableCollection<ReportScopeItem> Scopes { get; } = new()
        {
            new ReportScopeItem { Scope = ReportScope.Monthly, DisplayName = AppResources.Reports_ScopeMonthly },            
            new ReportScopeItem { Scope = ReportScope.Last3Months, DisplayName = AppResources.Reports_ScopeLast3Months },
            new ReportScopeItem { Scope = ReportScope.Last6Months, DisplayName = AppResources.Reports_ScopeLast6Months },
            new ReportScopeItem { Scope = ReportScope.Yearly, DisplayName = AppResources.Reports_ScopeYearly },
            new ReportScopeItem { Scope = ReportScope.Custom, DisplayName = AppResources.Reports_ScopeCustom }
        };

        // ================ przygotowanie do zakładek =====================
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCategoriesTabVisible))]
        [NotifyPropertyChangedFor(nameof(IsProjectsTabVisible))]
        [NotifyPropertyChangedFor(nameof(IsTrendTabVisible))]
        [NotifyPropertyChangedFor(nameof(IsWealthTabVisible))]
        [NotifyPropertyChangedFor(nameof(IsTopTabVisible))]
        public partial ReportTab SelectedTab { get; set; } = ReportTab.Categories;

        public bool IsCategoriesTabVisible => SelectedTab == ReportTab.Categories;
        public bool IsProjectsTabVisible => SelectedTab == ReportTab.Projects;
        public bool IsTrendTabVisible => SelectedTab == ReportTab.Trend;
        public bool IsWealthTabVisible => SelectedTab == ReportTab.Wealth;
        public bool IsTopTabVisible => SelectedTab == ReportTab.Top;

        [RelayCommand]
        private void SwitchReportTab(string tabName)
        {
            if (Enum.TryParse<ReportTab>(tabName, true, out var selectedTab))
                SelectedTab = selectedTab;
        }

        //========     koniec przygotowania zakładek =====================

        [ObservableProperty]
        public partial ObservableCollection<ISeries> ProjectSeries { get; set; } = new();

        [ObservableProperty]
        public partial ObservableCollection<TransactionDetailDto> TopExpenses { get; set; } = new();

        [ObservableProperty]
        public partial ReportScopeItem? SelectedScope { get; set; }

        private bool _isInitialized;
        private bool _isLoading;

        //-----------widoczność zakładek wykresów i pustego stanu w zależności od danych--------------------------------------

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsProjectEmptyStateVisible))]
        [NotifyPropertyChangedFor(nameof(IsProjectChartActuallyVisible))]
        public partial bool HasProjectData { get; set; }

        //[ObservableProperty]
        //[NotifyPropertyChangedFor(nameof(IsProjectEmptyStateVisible))]
        //[NotifyPropertyChangedFor(nameof(IsProjectChartActuallyVisible))]
        //public partial bool IsCategoryChartVisible { get; set; } = true;

        //[ObservableProperty]
        //[NotifyPropertyChangedFor(nameof(IsProjectEmptyStateVisible))]
        //[NotifyPropertyChangedFor(nameof(IsProjectChartActuallyVisible))]
        //public partial bool IsProjectChartVisible { get; set; } = false;
        public bool IsProjectEmptyStateVisible => !HasProjectData;
        public bool IsProjectChartActuallyVisible => HasProjectData;

        //============================koniec deklaracji pól i właściwości========================================================
        // TRENDY
        //[ObservableProperty]
        //public partial bool ShowIncomeTrend { get; set; } = true;

        //[ObservableProperty]
        //public partial bool ShowExpenseTrend { get; set; } = true;

        //[ObservableProperty]
        //public partial bool ShowNetTrend { get; set; } = true;

        // koniec Trendów

        public ReportsViewModel(IReportService reportService)
        {
            _reportService = reportService;
            // Ustawienie domyślne na Miesięczny nie wyzwala od razu ładowania (robimy to w InitializeAsync)
            //SelectedScope = Scopes.First();
        }

        public async Task InitializeAsync()
        {
            // Jeśli wracamy na stronę (nawigacja wstecz), tylko odświeżamy dane
            if (_isInitialized)
            {
                await UpdateRangeAndLoadAsync();
                return;
            }

            _isInitialized = true;
            _referenceDate = DateTime.Today;

            // Przypisanie tutaj wyzwoli OnSelectedScopeChanged, co automatycznie załaduje dane
            SelectedScope = Scopes.First();
        }

        // Reakcja na zmianę opcji w Pickerze z poziomu UI
        partial void OnSelectedScopeChanged(ReportScopeItem? value)
        {
            if (value == null) return;

            _referenceDate = DateTime.Today; // Powrót do "dziś" przy zmianie trybu
            _ = UpdateRangeAndLoadAsync();   // Bezpieczne odpalenie asynchroniczne typu Fire-and-forget (XAML je obsłuży)
        }

        //[RelayCommand]
        //private void SwitchChart(string chartType)
        //{
        //    if(chartType == "Category")
        //    {
        //        IsCategoryChartVisible = true;
        //        IsProjectChartVisible = false;
        //    }
        //    else if (chartType == "Project")
        //    {
        //        IsCategoryChartVisible = false;
        //        IsProjectChartVisible = true;
        //    }
        //}

        [RelayCommand]
        private async Task PreviousPeriodAsync()
        {
            ShiftReferenceDate(-1);
            await UpdateRangeAndLoadAsync();
        }

        [RelayCommand]
        private async Task NextPeriodAsync()
        {
            ShiftReferenceDate(1);
            await UpdateRangeAndLoadAsync();
        }

        [RelayCommand]
        private async Task ApplyCustomDateAsync()
        {
            // Ręczne wymuszenie załadowania danych dla wybranych z DatePickerów dat
            await LoadReportDataAsync();
        }

        private void ShiftReferenceDate(int direction)
        {
            if (SelectedScope?.Scope == ReportScope.Yearly)
                _referenceDate = _referenceDate.AddYears(direction);
            else if (SelectedScope?.Scope == ReportScope.Monthly)
                _referenceDate = _referenceDate.AddMonths(direction);
        }

        private async Task UpdateRangeAndLoadAsync()
        {
            if (SelectedScope == null || _isLoading) return;
            _isLoading = true;

            try
            {
                var today = DateTime.Today;

                // Resetowanie flag widoczności
                IsNavigationVisible = false;
                IsCustomDateVisible = false;

                switch (SelectedScope.Scope)
                {
                    case ReportScope.Monthly:
                        StartDate = new DateTime(_referenceDate.Year, _referenceDate.Month, 1);
                        EndDate = StartDate.AddMonths(1).AddDays(-1);
                        CurrentPeriodLabel = Capitalize(StartDate.ToString("MMMM yyyy", CultureInfo.CurrentCulture));
                        IsNavigationVisible = true;
                        break;

                    case ReportScope.Yearly:
                        StartDate = new DateTime(_referenceDate.Year, 1, 1);
                        EndDate = new DateTime(_referenceDate.Year, 12, 31);
                        CurrentPeriodLabel = string.Format(AppResources.Reports_YearLabel, _referenceDate.Year);
                        IsNavigationVisible = true;
                        break;

                    case ReportScope.Last3Months:
                        StartDate = new DateTime(today.Year, today.Month, 1).AddMonths(-2);
                        EndDate = new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);
                        CurrentPeriodLabel = $"{StartDate:MMM yyyy} - {EndDate:MMM yyyy}";
                        break;

                    case ReportScope.Last6Months:
                        StartDate = new DateTime(today.Year, today.Month, 1).AddMonths(-5);
                        EndDate = new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);
                        CurrentPeriodLabel = $"{StartDate:MMM yyyy} - {EndDate:MMM yyyy}";
                        break;

                    case ReportScope.Custom:
                        CurrentPeriodLabel = "Własny zakres";
                        IsCustomDateVisible = true;
                        // Ważne: Nie nadpisujemy StartDate i EndDate, pozwalamy użytkownikowi je wybrać!
                        break;
                }

                // Pomiń automatyczne ładowanie, jeśli użytkownik dopiero wybrał tryb "Custom" i musi ustawić daty
                if (SelectedScope.Scope != ReportScope.Custom)
                {
                    await LoadReportDataAsync();
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        private string Capitalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return char.ToUpper(text[0]) + text.Substring(1);
        }

        private async Task LoadReportDataAsync()
        {
            double innerRadius = DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Platform == DevicePlatform.iOS ? 30 : 60;
            var filterContext = new ReportFilterContext
            {
                StartDate = StartDate,
                EndDate = EndDate,
                TransactionType = TransactionType.Expense
                // Docelowo: BaseCurrency = _settingsService.GetBaseCurrency()
            };

            var categoryData = await _reportService.GetCategoryBreakdownAsync(filterContext);
            var projectData = await _reportService.GetProjectBreakdownAsync(filterContext);
            ProjectSeries.Clear();

            if (!projectData.Any(p => p.ProjectId != 0)) // Sprawdzamy czy są jakiekolwiek przypisane projekty
            {
                HasProjectData = false;
            }
            else
            {
                HasProjectData = true;
                //innerRadius = DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Platform == DevicePlatform.iOS ? 30 : 60;

                foreach (var item in projectData)
                {
                    // Ignorujemy z wykresu transakcje bez projektu (opcjonalne, w zależności od wymagań biznesowych)
                    if (item.ProjectId == 0) continue;

                    SKColor.TryParse(item.ColorHex, out var skColor);
                    ProjectSeries.Add(new PieSeries<double>
                    {
                        Values = new double[] { (double)item.TotalAmount },
                        Name = item.ProjectName,
                        Fill = new SolidColorPaint(skColor),
                        InnerRadius = innerRadius,
                        MaxRadialColumnWidth = 70,
                        HoverPushout = 10,
                        ToolTipLabelFormatter = point => $"{point.Context.Series.Name}: {point.Model:N2} ({item.Percentage:F1}%)"
                    });
                }
            }

            // CZYSZCZENIE KOLEKCJI ZAMIAST TWORZENIA NOWYCH INSTANCJI
            CategorySeries.Clear();
            TopExpenses.Clear();

            if (!categoryData.Any())
            {
                HasData = false;
                return;
            }

            HasData = true;

            // 1. Aktualizacja wykresu kołowego
            //double innerRadius = DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Platform == DevicePlatform.iOS ? 30 : 60;

            foreach (var item in categoryData)
            {
                SKColor.TryParse(item.ColorHex, out var skColor);
                CategorySeries.Add(new PieSeries<double>
                {
                    Values = new double[] { (double)item.TotalAmount },
                    Name = item.CategoryName,
                    Fill = new SolidColorPaint(skColor),
                    InnerRadius = innerRadius,
                    MaxRadialColumnWidth = 70, // Blokuje nadmierne "puchnięcie" wykresu na Windowsie
                    HoverPushout = 10, // Efekt wysunięcia przy najechaniu/dotknięciu
                    ToolTipLabelFormatter = point => $"{point.Context.Series.Name}: {point.Model:N2} ({item.Percentage:F1}%)"
                });
            }

            // 2. Aktualizacja największych wydatków
            var topTransactions = await _reportService.GetTopTransactionsAsync(filterContext, 10);
            foreach (var transaction in topTransactions)
            {
                TopExpenses.Add(transaction);
            }
        }
    }
}
