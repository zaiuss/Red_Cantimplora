namespace Cantimplora.Services;

// Contrato que la página de config (Esp32ConfigPage) consume del Esp32ViewModel.
// Define qué propiedades y métodos están visibles para esa página.
public interface IEsp32ConfigCoordinator
{
    int TxRepeat { get; }
    int GapMs { get; }
    bool ShowSystemMessages { get; }

    Task UpdateTxRepeatAsync(int value);
    Task UpdateGapAsync(int value);
    Task QueryEsp32SettingsAsync();
}