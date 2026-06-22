using WalkBase.ViewModels;

namespace WalkBase.Views;

public partial class QuestsPage : ContentPage
{
    private readonly QuestsViewModel _vm;

    public QuestsPage(QuestsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.RewardClaimed += AnimateToast;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
    }

    private async void AnimateToast()
    {
        Toast.IsVisible = true;
        Toast.Opacity = 0;
        await Toast.FadeTo(1, 180, Easing.CubicOut);
        await Task.Delay(1800);
        await Toast.FadeTo(0, 350);
        Toast.IsVisible = false;
    }
}
