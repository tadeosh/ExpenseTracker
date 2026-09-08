using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Services.Interfaces;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;

namespace ExpenseTracker.ViewModels
{
    public partial class ReportsViewModel : ObservableObject
    {
        private readonly IReportService _reportService;

        [ObservableProperty]
        public partial DateTime StartDate { get; set; }

        [ObservableProperty]
        public partial DateTime EndDate { get; set; }

        [ObservableProperty]
        public partial string CurrentPeriodLabel { get; set; } = string.Empty;

        // Kolekcja dla wykresu kołowego LiveCharts2
        [ObservableProperty]
        public partial ObservableCollection<ISeries> CategorySeries { get; set; } = new();

        [ObservableProperty]
        public partial bool HasData { get; set; } = true; // Flaga dla pustego stanu

        public ReportsViewModel(IReportService reportService)
        {
            _reportService = reportService;
            SetThisMonth();
        }

        [RelayCommand]
        private async Task LoadReportDataAsync()
        {
            var data = await _reportService.GetExpensesByCategoryAsync(StartDate, EndDate);

            // Obsługa pustego stanu (brak wydatków w danym miesiącu)
            if (!data.Any())
            {
                HasData = false;
                CategorySeries = new ObservableCollection<ISeries>();
                return;
            }

            HasData = true;
            var series = new ObservableCollection<ISeries>();

            foreach (var item in data)
            {
                SKColor.TryParse(item.ColorHex, out var skColor);

                series.Add(new PieSeries<double>
                {
                    Values = new double[] { (double)item.TotalAmount },
                    Name = item.CategoryName,
                    Fill = new SolidColorPaint(skColor),
                    InnerRadius = 50,
                    ToolTipLabelFormatter = point => $"{point.Context.Series.Name}: {point.Model:C2} ({item.Percentage:F1}%)"
                });
            }

            // Atomowa podmiana kolekcji
            CategorySeries = series;
        }

        [RelayCommand]
        private async Task SetThisMonth()
        {
            var today = DateTime.Today;
            StartDate = new DateTime(today.Year, today.Month, 1);
            EndDate = StartDate.AddMonths(1).AddDays(-1);
            CurrentPeriodLabel = "Bieżący miesiąc";
            await LoadReportDataAsync();
        }

        [RelayCommand]
        private async Task SetLastMonth()
        {
            var today = DateTime.Today;
            StartDate = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
            EndDate = StartDate.AddMonths(1).AddDays(-1);
            CurrentPeriodLabel = "Poprzedni miesiąc";
            await LoadReportDataAsync();
        }
    }
}
