namespace Cantimplora.Services;

// Encapsula el acceso a las settings del ESP32 (txRepeat, gapMs) en SQLite.
// Aísla las claves de BD detrás de una API con tipos.
public class SettingsService
{
    private const string KeyTxRepeat = "TxRepeat";
    private const string KeyGapMs = "GapMs";

    private const int DefaultTxRepeat = 4;
    private const int DefaultGapMs = 1000;

    private readonly IMessageStore _store;

    public SettingsService(IMessageStore store)
    {
        _store = store;
    }

    // Devuelve el número de repeticiones por mensaje guardado. Si no existe o no parsea, devuelve el default.
    public async Task<int> GetTxRepeatAsync()
    {
        var raw = await _store.GetSettingAsync(KeyTxRepeat);
        return int.TryParse(raw, out var value) ? value : DefaultTxRepeat;
    }

    // Guarda el número de repeticiones por mensaje.
    public Task SetTxRepeatAsync(int value) => _store.SetSettingAsync(KeyTxRepeat, value.ToString());

    // Devuelve el tiempo entre repeticiones (ms). Si no existe o no parsea, devuelve el default.
    public async Task<int> GetGapMsAsync()
    {
        var raw = await _store.GetSettingAsync(KeyGapMs);
        return int.TryParse(raw, out var value) ? value : DefaultGapMs;
    }

    // Guarda el tiempo entre repeticiones (ms).
    public Task SetGapMsAsync(int value) => _store.SetSettingAsync(KeyGapMs, value.ToString());
}