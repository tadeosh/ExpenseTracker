using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Models.Reports;
using ExpenseTracker.Resources.Strings;
using ExpenseTracker.Services;
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
        private readonly ISettingsService _settingsService;

        // Baza do obliczeń (punkt w czasie, po którym przesuwają się strzałki)
        private DateTime _referenceDate = DateTime.Today;
        //[ObservableProperty]
        //public partial string PageTitle { get; set; } = string.Empty;
        public string PageTitle => $"{AppResources.ReportsPageTitle} ({_settingsService.DefaultCurrency})";

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

        [ObservableProperty]
        private bool isCustomDateExpanded = true;

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
        [ObservableProperty]
        public partial bool HasCategoryData { get; set; }
        [ObservableProperty]
        public partial string CustomDateError { get; set; } = string.Empty;


        public bool IsProjectEmptyStateVisible => !HasProjectData;
        public bool IsProjectChartActuallyVisible => HasProjectData;

        //============================koniec deklaracji pól i właściwości========================================================
        // TRENDY
        [ObservableProperty]
        public partial ObservableCollection<ISeries> TrendSeries { get; set; } = new();

        [ObservableProperty]
        public partial Axis[] TrendXAxes { get; set; } = [new Axis()];

        [ObservableProperty]
        public partial Axis[] TrendYAxes { get; set; } = [new Axis()];

        [ObservableProperty]
        public partial bool ShowIncomeTrend { get; set; } = true;

        [ObservableProperty]
        public partial bool ShowExpenseTrend { get; set; } = true;

        [ObservableProperty]
        public partial bool ShowNetTrend { get; set; } = true;

        [ObservableProperty]
        public partial bool ShowWeekNumbers { get; set; } = true;

        [ObservableProperty]
        public partial bool ShowRoundedTrendAmounts { get; set; } = true;

        [ObservableProperty]
        public partial bool IsWeeklyTrend { get; set; }

        [ObservableProperty]
        public partial bool HasTrendData { get; set; }
        [ObservableProperty]
        public partial bool AutoScaleYAxis { get; set; } = true;

        private LineSeries<double>? _incomeTrendSeries;
        private LineSeries<double>? _expenseTrendSeries;
        private LineSeries<double>? _netTrendSeries;
        private IReadOnlyList<TrendDataPoint> _trendData = [];
        private TrendGrouping _currentTrendGrouping = TrendGrouping.Day;
        private bool _isCustomTrendRange;

        // koniec Trendów
        // ============================ MAJĄTEK ============================

        [ObservableProperty]
        public partial ObservableCollection<ISeries> WealthSeries { get; set; } = new();

        [ObservableProperty]
        public partial Axis[] WealthXAxes { get; set; } = [new Axis()];

        [ObservableProperty]
        public partial Axis[] WealthYAxes { get; set; } = [new Axis()];

        [ObservableProperty]
        public partial bool ShowWealthValue { get; set; } = true;

        [ObservableProperty]
        public partial bool ShowWealthChange { get; set; }

        [ObservableProperty]
        public partial bool ShowRoundedWealthAmounts { get; set; } = true;

        [ObservableProperty]
        public partial bool AutoScaleWealthYAxis { get; set; } = true;

        [ObservableProperty]
        public partial bool HasWealthData { get; set; }

        [ObservableProperty]
        public partial bool WealthRequiresAccountSetup { get; set; }

        [ObservableProperty]
        public partial string WealthErrorMessage { get; set; } = string.Empty;

        private LineSeries<double>? _wealthValueSeries;
        private LineSeries<double>? _wealthChangeSeries;

        private IReadOnlyList<BalanceHistoryPoint> _wealthData = [];

        private TrendGrouping _currentWealthGrouping = TrendGrouping.Day;

        private bool _isCustomWealthRange;

        // ======================== KONIEC MAJĄTKU =========================

        public ReportsViewModel(IReportService reportService, ISettingsService settingsService)
        {
            _reportService = reportService;
            _settingsService = settingsService;

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

        [RelayCommand]
        private void ToggleCustomDate()
        {
            IsCustomDateExpanded = !IsCustomDateExpanded;
        }

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
            CustomDateError = string.Empty;

            var startDate = StartDate.Date;
            var endDate = EndDate.Date;

            if (startDate > endDate)
            {
                CustomDateError = AppResources.Reports_InvalidDateRange;
                IsCustomDateExpanded = true;
                return;
            }

            try
            {
                await LoadReportDataAsync();

                CurrentPeriodLabel = FormatCustomPeriodLabel(startDate, endDate);
                IsCustomDateExpanded = false;
            }
            catch (Exception)
            {
                CustomDateError = AppResources.Reports_GenerateError;
                IsCustomDateExpanded = true;
            }
        }

        private void ShiftReferenceDate(int direction)
        {
            if (SelectedScope?.Scope == ReportScope.Yearly)
                _referenceDate = _referenceDate.AddYears(direction);
            else if (SelectedScope?.Scope == ReportScope.Monthly)
                _referenceDate = _referenceDate.AddMonths(direction);
        }

        private static string FormatCustomPeriodLabel(DateTime startDate, DateTime endDate)
        {
            return $"{startDate.ToString("d", CultureInfo.CurrentCulture)} – {endDate.ToString("d", CultureInfo.CurrentCulture)}";
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
                        IsCustomDateExpanded = true;
                        CustomDateError = string.Empty;
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

            var expenseContext = new ReportFilterContext
            {
                StartDate = StartDate,
                EndDate = EndDate,
                TransactionType = TransactionType.Expense,
                BaseCurrency = _settingsService.DefaultCurrency
            };

            var trendContext = new ReportFilterContext
            {
                StartDate = StartDate,
                EndDate = EndDate,
                TransactionType = null,
                BaseCurrency = _settingsService.DefaultCurrency
            };

            var categoryData = await _reportService.GetCategoryBreakdownAsync(expenseContext);
            var projectData = await _reportService.GetProjectBreakdownAsync(expenseContext);
            var topTransactions = await _reportService.GetTopTransactionsAsync(expenseContext, 10);

            await LoadTrendAsync(trendContext, SelectedScope?.Scope == ReportScope.Custom);
            await LoadWealthAsync(trendContext, SelectedScope?.Scope == ReportScope.Custom);

            CategorySeries.Clear();
            ProjectSeries.Clear();
            TopExpenses.Clear();

            HasCategoryData = categoryData.Any();
            HasProjectData = projectData.Any(x => x.ProjectId != 0);

            foreach (var item in categoryData)
            {
                SKColor.TryParse(item.ColorHex, out var skColor);

                CategorySeries.Add(new PieSeries<double>
                {
                    Values = [(double)item.TotalAmount],
                    Name = item.CategoryName,
                    Fill = new SolidColorPaint(skColor),
                    InnerRadius = innerRadius,
                    MaxRadialColumnWidth = 70,
                    HoverPushout = 10,
                    ToolTipLabelFormatter = point => $"{point.Context.Series.Name}: {point.Model:N2} ({item.Percentage:F1}%)"
                });
            }

            if (HasProjectData)
            {
                foreach (var item in projectData.Where(x => x.ProjectId != 0))
                {
                    SKColor.TryParse(item.ColorHex, out var skColor);

                    ProjectSeries.Add(new PieSeries<double>
                    {
                        Values = [(double)item.TotalAmount],
                        Name = item.ProjectName,
                        Fill = new SolidColorPaint(skColor),
                        InnerRadius = innerRadius,
                        MaxRadialColumnWidth = 70,
                        HoverPushout = 10,
                        ToolTipLabelFormatter = point => $"{point.Context.Series.Name}: {point.Model:N2} ({item.Percentage:F1}%)"
                    });
                }
            }

            foreach (var transaction in topTransactions)
                TopExpenses.Add(transaction);

            HasData = categoryData.Any() || projectData.Any(x => x.ProjectId != 0) || topTransactions.Any() || HasTrendData || HasWealthData;
        }

        // ================ TRENDY ==========================
        partial void OnShowIncomeTrendChanged(bool value)
        {
            RefreshVisibleTrendSeries();
        }

        partial void OnShowExpenseTrendChanged(bool value)
        {
            RefreshVisibleTrendSeries();
        }

        partial void OnShowNetTrendChanged(bool value)
        {
            RefreshVisibleTrendSeries();
        }

        partial void OnShowWeekNumbersChanged(bool value)
        {
            RefreshTrendAxes();
            RefreshWealthAxes();
        }

        private static SKColor GetChartColor(string resourceKey, string fallbackHex)
        {
            if (Application.Current?.Resources.TryGetValue(resourceKey, out var resource) == true &&
                resource is Color color)
            {
                return new SKColor(
                    (byte)(color.Red * 255),
                    (byte)(color.Green * 255),
                    (byte)(color.Blue * 255),
                    (byte)(color.Alpha * 255));
            }

            return SKColor.Parse(fallbackHex);
        }

        partial void OnShowRoundedTrendAmountsChanged(bool value)
        {
            RefreshTrendAxes();
        }
        partial void OnAutoScaleYAxisChanged(bool value)
        {
            RefreshTrendAxes();
        }
        
        private async Task LoadTrendAsync(ReportFilterContext context, bool isCustomRange)
        {
            var trendData = await _reportService.GetTrendAsync(context);

            _isCustomTrendRange = isCustomRange;

            _trendData = trendData;
            _currentTrendGrouping = ReportService.GetTrendGrouping(context.StartDate, context.EndDate);
            IsWeeklyTrend = _currentTrendGrouping == TrendGrouping.Week;
            HasTrendData = trendData.Any(x => x.Income != 0m || x.Expense != 0m);

            var incomeColor = GetChartColor("IncomeChartColor","#2E8B57");
            var expenseColor = GetChartColor("ExpenseChartColor","#D9534F");
            var netColor = GetChartColor("NetChartColor","#1E88E5");

            _incomeTrendSeries = new LineSeries<double>
            {
                Name = AppResources.Reports_TrendIncome,
                Values = trendData.Select(x => (double)x.Income).ToArray(),
                Stroke = new SolidColorPaint(incomeColor) { StrokeThickness = 3 },
                GeometryStroke = new SolidColorPaint(incomeColor) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(incomeColor),
                GeometrySize = 7,
                Fill = null,
                LineSmoothness = 0.25,
                //ToolTipLabelFormatter = point => $"{AppResources.Reports_TrendIncome}: {point.PrimaryValue:N2} {_settingsService.DefaultCurrency}"
            };

            _expenseTrendSeries = new LineSeries<double>
            {
                Name = AppResources.Reports_TrendExpenses,
                Values = trendData.Select(x => (double)x.Expense).ToArray(),
                Stroke = new SolidColorPaint(expenseColor) { StrokeThickness = 3 },
                GeometryStroke = new SolidColorPaint(expenseColor) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(expenseColor),
                GeometrySize = 7,
                Fill = null,
                LineSmoothness = 0.25,
                //ToolTipLabelFormatter = point => $"{AppResources.Reports_TrendExpenses}: {point.PrimaryValue:N2} {_settingsService.DefaultCurrency}"
            };

            _netTrendSeries = new LineSeries<double>
            {
                Name = AppResources.Reports_TrendBalance,
                Values = trendData.Select(x => (double)(x.Income - x.Expense)).ToArray(),
                Stroke = new SolidColorPaint(netColor) { StrokeThickness = 3 },
                GeometryStroke = new SolidColorPaint(netColor) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(netColor),
                GeometrySize = 7,
                Fill = null,
                LineSmoothness = 0.25,
                //ToolTipLabelFormatter = point => $"{AppResources.Reports_TrendBalance}: {point.PrimaryValue:N2} {_settingsService.DefaultCurrency}"
            };

            RefreshVisibleTrendSeries();
            RefreshTrendAxes();
        }

        private void RefreshVisibleTrendSeries()
        {
            TrendSeries.Clear();

            if (ShowIncomeTrend && _incomeTrendSeries is not null)
                TrendSeries.Add(_incomeTrendSeries);

            if (ShowExpenseTrend && _expenseTrendSeries is not null)
                TrendSeries.Add(_expenseTrendSeries);

            if (ShowNetTrend && _netTrendSeries is not null)
                TrendSeries.Add(_netTrendSeries);
        }

        private double GetTrendMaxValue()
        {
            if (_trendData.Count == 0)
                return 10;

            var maxValue =
                Math.Max(
                    _trendData.Max(x => (double)x.Income),
                    Math.Max(
                        _trendData.Max(x => (double)x.Expense),
                        _trendData.Max(x => (double)Math.Abs(x.Income - x.Expense))));

            return maxValue <= 0
                ? 10
                : maxValue;
        }

        private void RefreshTrendAxes()
        {
            var labels = _trendData.Select(CreateTrendLabel).ToArray();

            TrendXAxes =
            [
                new Axis
                {
                    Labels = labels,
                    LabelsRotation = 90,
                    TextSize = 10,
                    MinStep = 1,
                    ForceStepToMin = true
                }
            ];

            var maxValue = GetTrendMaxValue();

            if (maxValue <= 0)
                maxValue = 10;

            TrendYAxes =
            [
                new Axis
                {
                    TextSize = 10,
                    Labeler = FormatTrendAmount,
                    MinLimit = AutoScaleYAxis ? null : 0,
                    MaxLimit = AutoScaleYAxis
                        ? null
                        : Math.Ceiling(maxValue * 1.1)
                }
            ];
        }

        private string FormatTrendAmount(double value)
        {
            return value.ToString(ShowRoundedTrendAmounts ? "N0" : "N2", CultureInfo.CurrentCulture);
        }

        private string CreateTrendLabel(TrendDataPoint point)
        {
            return _currentTrendGrouping switch
            {
                TrendGrouping.Day when _isCustomTrendRange => CreateCustomDayLabel(point.Date),
                TrendGrouping.Day => point.Date.Day.ToString(CultureInfo.CurrentCulture),
                TrendGrouping.Week when ShowWeekNumbers => $"W{ISOWeek.GetWeekOfYear(point.Date):00}",
                TrendGrouping.Week => CreateWeekRangeLabel(point.Date),
                TrendGrouping.Month when _isCustomTrendRange => point.Date.ToString("MMM yy", CultureInfo.CurrentCulture),
                TrendGrouping.Month => point.Date.ToString("MMM", CultureInfo.CurrentCulture),
                _ => point.Date.ToShortDateString()
            };
        }

        private string CreateCustomDayLabel(DateTime date)
        {
            var crossesYears = _trendData.Count > 0 && _trendData.First().Date.Year != _trendData.Last().Date.Year;
            return date.ToString(crossesYears ? "dd.MM.yy" : "dd.MM", CultureInfo.CurrentCulture);
        }

        private string CreateWeekRangeLabel(DateTime weekStart)
        {
            var weekEnd = weekStart.AddDays(6);
            var crossesYears = weekStart.Year != weekEnd.Year || (_trendData.Count > 0 && _trendData.First().Date.Year != _trendData.Last().Date.Year);

            if (crossesYears)
                return $"{weekStart:dd.MM.yy}-{weekEnd:dd.MM.yy}";

            if (weekStart.Month != weekEnd.Month)
                return $"{weekStart:dd.MM}-{weekEnd:dd.MM}";

            return $"{weekStart:dd}-{weekEnd:dd.MM}";
        }

        // ============================ MAJĄTEK ============================

        private async Task LoadWealthAsync(
            ReportFilterContext context,
            bool isCustomRange)
        {
            WealthErrorMessage = string.Empty;
            WealthRequiresAccountSetup = false;

            try
            {
                var wealthData =
                    await _reportService.GetBalanceHistoryAsync(context);

                _wealthData = wealthData;

                _isCustomWealthRange = isCustomRange;

                _currentWealthGrouping =
                    ReportService.GetTrendGrouping(
                        context.StartDate,
                        context.EndDate);

                HasWealthData = wealthData.Count > 0;

                if (!HasWealthData)
                {
                    _wealthValueSeries = null;
                    _wealthChangeSeries = null;

                    WealthSeries.Clear();

                    RefreshWealthAxes();

                    return;
                }

                var balanceValues = wealthData
                    .Select(point => (double)point.Balance)
                    .ToArray();

                var changeValues = new double[wealthData.Count];

                for (var i = 0; i < wealthData.Count; i++)
                {
                    if (i == 0)
                    {
                        changeValues[i] = 0d;
                        continue;
                    }

                    changeValues[i] =
                        (double)(
                            wealthData[i].Balance -
                            wealthData[i - 1].Balance);
                }

                var wealthColor = GetChartColor("WealthChartColor","#1E88E5");
                var changeColor = GetChartColor("WealthChangeChartColor","#8E44AD");

                _wealthValueSeries = new LineSeries<double>
                {
                    Name = AppResources.Reports_WealthValue,
                    Values = balanceValues,

                    Stroke = new SolidColorPaint(wealthColor)
                    {
                        StrokeThickness = 3
                    },

                    GeometryStroke = new SolidColorPaint(wealthColor)
                    {
                        StrokeThickness = 2
                    },

                    GeometryFill = new SolidColorPaint(wealthColor),

                    GeometrySize = 7,

                    Fill = null,

                    LineSmoothness = 0.25
                };

                _wealthChangeSeries = new LineSeries<double>
                {
                    Name = AppResources.Reports_WealthChange,
                    Values = changeValues,

                    Stroke = new SolidColorPaint(changeColor)
                    {
                        StrokeThickness = 3
                    },

                    GeometryStroke = new SolidColorPaint(changeColor)
                    {
                        StrokeThickness = 2
                    },

                    GeometryFill = new SolidColorPaint(changeColor),

                    GeometrySize = 7,

                    Fill = null,

                    LineSmoothness = 0.25
                };

                RefreshVisibleWealthSeries();
                RefreshWealthAxes();
            }
            catch (InvalidOperationException ex)
            {
                // Obecnie GetBalanceHistoryAsync rzuca
                // InvalidOperationException m.in. gdy konto nie ma
                // InitialBalanceDate albo brakuje kursu historycznego.

                _wealthData = [];

                _wealthValueSeries = null;
                _wealthChangeSeries = null;

                HasWealthData = false;

                WealthSeries.Clear();

                WealthRequiresAccountSetup = true;
                WealthErrorMessage = ex.Message;

                RefreshWealthAxes();
            }
        }

        partial void OnShowWealthValueChanged(bool value)
        {
            RefreshVisibleWealthSeries();
        }

        partial void OnShowWealthChangeChanged(bool value)
        {
            RefreshVisibleWealthSeries();
        }

        partial void OnShowRoundedWealthAmountsChanged(bool value)
        {
            RefreshWealthAxes();
        }

        partial void OnAutoScaleWealthYAxisChanged(bool value)
        {
            RefreshWealthAxes();
        }

        private void RefreshVisibleWealthSeries()
        {
            WealthSeries.Clear();

            if (ShowWealthValue &&
                _wealthValueSeries is not null)
            {
                WealthSeries.Add(_wealthValueSeries);
            }

            if (ShowWealthChange &&
                _wealthChangeSeries is not null)
            {
                WealthSeries.Add(_wealthChangeSeries);
            }
        }

        private string CreateWealthLabel(BalanceHistoryPoint point)
        {
            return _currentWealthGrouping switch
            {
                TrendGrouping.Day when _isCustomWealthRange =>
                    CreateWealthCustomDayLabel(point.Date),

                TrendGrouping.Day =>
                    point.Date.Day.ToString(
                        CultureInfo.CurrentCulture),

                TrendGrouping.Week when ShowWeekNumbers =>
                    $"W{ISOWeek.GetWeekOfYear(point.Date):00}",

                TrendGrouping.Week =>
                    CreateWealthWeekRangeLabel(point.Date),

                TrendGrouping.Month when _isCustomWealthRange =>
                    point.Date.ToString(
                        "MMM yy",
                        CultureInfo.CurrentCulture),

                TrendGrouping.Month =>
                    point.Date.ToString(
                        "MMM",
                        CultureInfo.CurrentCulture),

                _ => point.Date.ToShortDateString()
            };
        }

        private string CreateWealthCustomDayLabel(DateTime date)
        {
            var crossesYears =
                _wealthData.Count > 0 &&
                _wealthData.First().Date.Year !=
                _wealthData.Last().Date.Year;

            return date.ToString(
                crossesYears
                    ? "dd.MM.yy"
                    : "dd.MM",
                CultureInfo.CurrentCulture);
        }

        private string CreateWealthWeekRangeLabel(DateTime weekStart)
        {
            var weekEnd = weekStart.AddDays(6);

            var crossesYears =
                weekStart.Year != weekEnd.Year ||
                (
                    _wealthData.Count > 0 &&
                    _wealthData.First().Date.Year !=
                    _wealthData.Last().Date.Year
                );

            if (crossesYears)
            {
                return
                    $"{weekStart:dd.MM.yy}-" +
                    $"{weekEnd:dd.MM.yy}";
            }

            if (weekStart.Month != weekEnd.Month)
            {
                return
                    $"{weekStart:dd.MM}-" +
                    $"{weekEnd:dd.MM}";
            }

            return
                $"{weekStart:dd}-" +
                $"{weekEnd:dd.MM}";
        }

        private void RefreshWealthAxes()
        {
            var labels = _wealthData
                .Select(CreateWealthLabel)
                .ToArray();

            WealthXAxes =
            [
               new Axis
                {
                    Labels = labels,
                    LabelsRotation = 90,
                    TextSize = 10,
                    MinStep = 1,
                    ForceStepToMin = true
                }
            ];

            var maxAbsoluteValue = GetWealthMaxAbsoluteValue();

            WealthYAxes =
            [
                new Axis
        {
            TextSize = 10,

            Labeler = FormatWealthAmount,

            MinLimit = AutoScaleWealthYAxis
                ? null
                : GetFixedWealthMinLimit(
                    maxAbsoluteValue),

            MaxLimit = AutoScaleWealthYAxis
                ? null
                : Math.Ceiling(
                    maxAbsoluteValue * 1.1)
        }
            ];
        }

        private double GetWealthMaxAbsoluteValue()
        {
            if (_wealthData.Count == 0)
                return 10d;

            var maxBalance = _wealthData
                .Max(point =>
                    Math.Abs((double)point.Balance));

            var maxChange = 0d;

            for (var i = 1; i < _wealthData.Count; i++)
            {
                var change = Math.Abs(
                    (double)(
                        _wealthData[i].Balance -
                        _wealthData[i - 1].Balance));

                if (change > maxChange)
                    maxChange = change;
            }

            var maxValue = Math.Max(
                maxBalance,
                maxChange);

            return maxValue <= 0d
                ? 10d
                : maxValue;
        }

        private double GetFixedWealthMinLimit(double maxAbsoluteValue)
        {
            var hasNegativeValue =
                _wealthData.Any(point =>
                    point.Balance < 0m);

            if (!hasNegativeValue)
            {
                for (var i = 1; i < _wealthData.Count; i++)
                {
                    if (
                        _wealthData[i].Balance -
                        _wealthData[i - 1].Balance < 0m)
                    {
                        hasNegativeValue = true;
                        break;
                    }
                }
            }

            return hasNegativeValue
                ? -Math.Ceiling(maxAbsoluteValue * 1.1)
                : 0d;
        }

        private string FormatWealthAmount(double value)
        {
            return value.ToString(
                ShowRoundedWealthAmounts
                    ? "N0"
                    : "N2",
                CultureInfo.CurrentCulture);
        }


    }
}
