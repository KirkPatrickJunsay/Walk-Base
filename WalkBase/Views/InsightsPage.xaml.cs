using SkiaSharp.Views.Maui;
using WalkBase.ViewModels;

namespace WalkBase.Views;

public partial class InsightsPage : ContentPage
{
    private readonly InsightsViewModel _vm;

    public InsightsPage(InsightsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        Chart.PaintSurface += OnPaintChart;
        _vm.ChartChanged += () => Chart.InvalidateSurface();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
    }

    private void OnPaintChart(object? sender, SKPaintSurfaceEventArgs e)
    {
        _vm.Chart.Draw(e.Surface.Canvas, e.Info, _vm.Bars, _vm.Goal);
    }
}
