using WalkBase.ViewModels;

namespace WalkBase.Views;

public partial class BuildPage : ContentPage
{
    private readonly BuildViewModel _vm;

    public BuildPage(BuildViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.ToastRequested += ShowToast;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
    }

    private async void ShowToast()
    {
        Toast.BackgroundColor = _vm.ToastIsSuccess
            ? Color.FromArgb("#234E36")   // green-tinted success
            : Color.FromArgb("#232F43");  // neutral
        Toast.IsVisible = true;
        Toast.Opacity = 0;
        Toast.TranslationY = 24;
        await Task.WhenAll(Toast.FadeTo(1, 150), Toast.TranslateTo(0, 0, 200, Easing.CubicOut));
        await Task.Delay(1700);
        await Toast.FadeTo(0, 200);
        Toast.IsVisible = false;
    }
}
