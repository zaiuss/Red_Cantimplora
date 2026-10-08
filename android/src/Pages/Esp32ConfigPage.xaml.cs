using Cantimplora.Services;

namespace Cantimplora.Pages;

public partial class Esp32ConfigPage : ContentPage
{
    private readonly IEsp32ConfigCoordinator _vm;
    private bool _isQuerying;

    public Esp32ConfigPage(IEsp32ConfigCoordinator vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_isQuerying) return;
        _isQuerying = true;
        try
        {
            await _vm.QueryEsp32SettingsAsync();
        }
        finally
        {
            _isQuerying = false;
        }
    }

    private async void OnTxUnfocused(object? sender, FocusEventArgs e)
    {
        if (int.TryParse(TxEntry.Text, out var value))
        {
            await _vm.UpdateTxRepeatAsync(value);
        }
    }

    private async void OnGapUnfocused(object? sender, FocusEventArgs e)
    {
        if (int.TryParse(GapEntry.Text, out var value))
        {
            await _vm.UpdateGapAsync(value);
        }
    }
}