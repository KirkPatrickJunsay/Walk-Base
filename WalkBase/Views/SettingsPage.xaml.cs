using WalkBase.ViewModels;

namespace WalkBase.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _vm;

    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadCommand.Execute(null);
    }

    // Restoring overwrites the whole save, so confirm first — a stray tap shouldn't wipe a town.
    private async void OnRestoreClicked(object? sender, EventArgs e)
    {
        bool ok = await DisplayAlert(
            "Restore from backup?",
            "This replaces your current town with the one in the backup file. Your current progress will be overwritten and can't be recovered.",
            "Restore", "Cancel");
        if (ok)
            await _vm.ImportDataCommand.ExecuteAsync(null);
    }
}
