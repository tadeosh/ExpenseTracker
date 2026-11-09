using Microsoft.Maui.Graphics;

namespace ExpenseTracker.Models.Reports;

public sealed record ReportPieLegendItem(int Id, string Name, string ColorHex, decimal Amount, double Percentage, bool CanDrillDown)
{
    public Color Color => Color.FromArgb(ColorHex);
}
