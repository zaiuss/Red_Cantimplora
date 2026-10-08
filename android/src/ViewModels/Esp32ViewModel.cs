using System.Collections.ObjectModel;
using System.Windows.Input;
using Cantimplora.Models;
using Cantimplora.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cantimplora.ViewModels;

// VM principal de la app. Gestiona conexión BT, lista de dispositivos, chat y persistencia.
// Implementa IChatCoordinator (para Esp32Page) e IEsp32ConfigCoordinator (para Esp32ConfigPage).
public partial class Esp32ViewModel : ObservableObject, IChatCoordinator, IEsp32ConfigCoordinator
{
    private readonly IBluetoothService _bt;
    private readonly IMessageStore _store;
    private readonly SettingsService _settings;
    private readonly INavigationService _navigation;
    private readonly IPermissionService _permissions;
    private readonly Esp32LineParser _lineParser = new();
    private readonly Esp32StatParser _statParser = new();
    private readonly ChatFilter _filter = new();
    private ChatBuffer _chat = null!;

    [ObservableProperty]
    private string status = "Desconectado";

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private bool isNotConnected = true;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string connectedName = string.Empty;

    [ObservableProperty]
    private string connectedMac = string.Empty;

    [ObservableProperty]
    private bool isDisconnecting;

    [ObservableProperty]
    private string draftMessage = string.Empty;

    [ObservableProperty]
    private string esp32Id = string.Empty;

    [ObservableProperty]
    private bool showSystemMessages = false;

    [ObservableProperty]
    private int txRepeat = 4;

    [ObservableProperty]
    private int gapMs = 1000;

    [ObservableProperty]
    private bool isRefreshing;

    public ObservableCollection<BluetoothDevice> Devices { get; } = new();
    public ObservableCollection<ChatMessage> Messages { get; } = new();

    // Reconstruye la lista visible aplicando el filtro con el flag actual.
    partial void OnShowSystemMessagesChanged(bool value)
    {
        _chat.Rebuild(value);
    }

    // Sincroniza flags derivados (IsNotConnected) y limpia estado al desconectar.
    partial void OnIsConnectedChanged(bool value)
    {
        IsNotConnected = !value;
        if (!value)
        {
            ConnectedName = string.Empty;
            ConnectedMac = string.Empty;
            Esp32Id = string.Empty;
            Devices.Clear();
        }
        NavigateToEsp32ConfigCommand.NotifyCanExecuteChanged();
    }

    // Inicializa el VM, suscribe a eventos del servicio BT y arranca la BD.
    public Esp32ViewModel(
        IBluetoothService bt,
        IMessageStore store,
        SettingsService settings,
        INavigationService navigation,
        IPermissionService permissions)
    {
        _bt = bt;
        _store = store;
        _settings = settings;
        _navigation = navigation;
        _permissions = permissions;
        _chat = new ChatBuffer(Messages, _filter);
        Devices.CollectionChanged += (_, _) =>
{
    OnPropertyChanged(nameof(HasDevices));
    OnPropertyChanged(nameof(HasNoDevices));
};

        _ = _store.InitializeAsync();

        _bt.ConnectionChanged += (_, connected) => OnConnectionChanged(connected);
        _bt.MessageReceived += (_, line) => OnMessageReceived(line);
    }

    // Actualiza IsConnected y Status cuando el servicio BT cambia de estado.
    private void OnConnectionChanged(bool connected)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsConnected = connected;
            Status = connected ? "Conectado" : "Desconectado";
        });
    }

    // Parsea una línea del ESP32, la añade al chat y la persiste en SQLite.
    private async void OnMessageReceived(string line)
    {
        var msg = _lineParser.Parse(line);
        await MainThread.InvokeOnMainThreadAsync(() => _chat.Add(msg, ShowSystemMessages));

        if (string.IsNullOrEmpty(ConnectedMac)) return;

        try
        {
            await _store.SaveAsync(BuildStoredMessage(
                msg.Direction, msg.Source ?? string.Empty, msg.Text, msg.Timestamp));
        }
        catch (Exception ex)
        {
            // Si falla la persistencia, el mensaje ya está visible en el chat pero no se guarda.
            // Se loguea para diagnóstico pero no se interrumpe el flujo del BT.
            System.Diagnostics.Debug.WriteLine($"[Cantimplora] SaveAsync falló: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Pide permisos BT, carga dispositivos emparejados del sistema y los muestra en la lista.
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;
        try
        {
            // Pequena espera para que el binding IsRefreshing=true se aplique
            // y el overlay se renderice antes de empezar la carga.
            await Task.Delay(50);

            if (!await EnsureBluetoothPermissionAsync()) return;

            Devices.Clear();
            foreach (var d in _bt.GetBondedDevices())
                Devices.Add(d);
            Status = $"Encontrados {Devices.Count}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    // Conecta con un dispositivo emparejado, carga el histórico de chat, lanza la query de settings.
    [RelayCommand]
    private async Task ConnectAsync(string? macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress)) return;
        if (IsBusy) return;

        if (!await EnsureBluetoothPermissionAsync()) return;

        var device = FindDevice(macAddress);
        if (device is null) return;

        try
        {
            ApplyConnectionState(device);
            await _bt.ConnectAsync(macAddress);
            await LoadSettingsFromDatabaseAsync();
            await LoadChatHistoryAsync(macAddress);
            await QueryEsp32SettingsAsync();
        }
        catch (Exception ex)
        {
            Status = "Error: " + ex.Message;
            ConnectedName = string.Empty;
            ConnectedMac = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Pide permiso BT y actualiza Status si falla. Devuelve true si hay permiso.
    private async Task<bool> EnsureBluetoothPermissionAsync()
    {
        var ok = await _permissions.EnsureBluetoothAsync();
        if (!ok)
        {
            Status = "Sin permisos de Bluetooth";
            return false;
        }
        return true;
    }

    // Busca el dispositivo en la lista local por MAC. Devuelve null si no existe.
    private BluetoothDevice? FindDevice(string macAddress) =>
        Devices.FirstOrDefault(d => d.MacAddress == macAddress);

    // Marca el VM como ocupado y guarda los datos del dispositivo antes de conectar.
    private void ApplyConnectionState(BluetoothDevice device)
    {
        IsBusy = true;
        Status = "Conectando...";
        ConnectedName = device.Name;
        ConnectedMac = device.MacAddress;
    }

    // Limpia el estado de conexión cuando algo sale mal.
    private void ClearConnectionState()
    {
        ConnectedName = string.Empty;
        ConnectedMac = string.Empty;
    }

    // Limpia el chat y lo rellena con el histórico de la MAC indicada.
    private async Task LoadChatHistoryAsync(string macAddress)
    {
        var history = await _store.LoadAsync(macAddress);
        await MainThread.InvokeOnMainThreadAsync(() => _chat.LoadFrom(history, RestoreChatMessage, ShowSystemMessages));
    }

    // Carga los valores guardados de SQLite (txRepeat, gapMs) al VM.
    private async Task LoadSettingsFromDatabaseAsync()
    {
        TxRepeat = await _settings.GetTxRepeatAsync();
        GapMs = await _settings.GetGapMsAsync();
    }

    // Pide /stat al ESP32 y actualiza Esp32Id, TxRepeat y GapMs. Persiste en SQLite.
    // No hace nada si no hay conexión.
    public async Task QueryEsp32SettingsAsync()
    {
        if (!IsConnected) return;

        var line = await WaitForOkStatLineAsync(TimeSpan.FromSeconds(5));
        if (line is null) return;

        if (_statParser.TryParseStatLine(line, out var id, out var tx, out var gap))
            ApplyStatResponse(id, tx, gap);

        await PersistCurrentSettingsAsync();
    }

    // Manda /stat al ESP32 y devuelve la primera línea OK con id=/tx= que llegue,
    // o null si vence el timeout sin respuesta.
    private async Task<string?> WaitForOkStatLineAsync(TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<string?>();
        EventHandler<string>? handler = null;
        handler = (_, incomingLine) =>
        {
            if (incomingLine.StartsWith("OK ", StringComparison.Ordinal))
            {
                tcs.TrySetResult(incomingLine);
                if (handler is not null)
                    _bt.MessageReceived -= handler;
            }
        };
        _bt.MessageReceived += handler;
        try
        {
            await _bt.SendAsync("/stat");
            using var cts = new CancellationTokenSource(timeout);
            await using var reg = cts.Token.Register(() => tcs.TrySetResult(null));
            return await tcs.Task;
        }
        finally
        {
            _bt.MessageReceived -= handler;
        }
    }

    // Aplica los valores de /stat al VM. Ignora campos vacíos o cero.
    private void ApplyStatResponse(string id, int tx, int gap)
    {
        if (!string.IsNullOrEmpty(id)) Esp32Id = id;
        if (tx > 0) TxRepeat = tx;
        if (gap > 0) GapMs = gap;
    }

    // Persiste los valores actuales de TxRepeat y GapMs en SQLite.
    private Task PersistCurrentSettingsAsync()
        => Task.WhenAll(
            _settings.SetTxRepeatAsync(TxRepeat),
            _settings.SetGapMsAsync(GapMs));

    // Actualiza el número de repeticiones por mensaje (manda /tx N al ESP32 y guarda en SQLite).
    public async Task UpdateTxRepeatAsync(int value)
    {
        TxRepeat = value;
        await _bt.SendAsync($"/tx {value}");
        await _settings.SetTxRepeatAsync(value);
    }

    // Actualiza el tiempo entre repeticiones (manda /gap N al ESP32 y guarda en SQLite).
    public async Task UpdateGapAsync(int value)
    {
        GapMs = value;
        await _bt.SendAsync($"/gap {value}");
        await _settings.SetGapMsAsync(value);
    }

    // Pide desconexión al servicio BT, mostrando overlay intermedio.
    [RelayCommand]
    private async Task DisconnectAsync()
    {
        IsDisconnecting = true;
        Status = "Desconectando...";
        await Task.Yield();
        _bt.Disconnect();
        IsDisconnecting = false;
    }

    // Evento para mostrar avisos en la UI (toasts).
    public event EventHandler<string>? InfoMessage;

    // Envía el texto del Entry por BT y lo registra como mensaje TX propio.
    [RelayCommand]
    private async Task SendAsync()
    {
        if (!IsConnected) return;
        if (!TryBuildDraft(out var text, out var isCommand)) return;
        if (ShouldBlockCommand(isCommand)) return;
        await SendAndPersistAsync(text, isCommand);
    }

    // Lee el draft del Entry. Devuelve false si está vacío o en blanco.
    private bool TryBuildDraft(out string text, out bool isCommand)
    {
        text = DraftMessage;
        isCommand = text.StartsWith("/");
        return !string.IsNullOrWhiteSpace(text);
    }

    // Si el texto es un comando y no estamos en modo debug, avisa y devuelve true (bloqueado).
    private bool ShouldBlockCommand(bool isCommand)
    {
        if (isCommand && !ShowSystemMessages)
        {
            InfoMessage?.Invoke(this, "cmd solo en debug mode");
            return true;
        }
        return false;
    }

    // Manda el texto por BT, lo registra como TX propio, lo guarda en SQLite y limpia el draft.
    // Si falla, registra un mensaje ERR en el chat.
    private async Task SendAndPersistAsync(string text, bool isCommand)
    {
        try
        {
            await _bt.SendAsync(text);
            var chatMsg = new ChatMessage(DateTime.Now, "TX", text) { Kind = "TX", Source = Esp32Id, IsCommand = isCommand };

            await MainThread.InvokeOnMainThreadAsync(() => _chat.Add(chatMsg, ShowSystemMessages));
            await _store.SaveAsync(BuildStoredMessage("TX", Esp32Id, text, chatMsg.Timestamp));
            DraftMessage = string.Empty;
        }
        catch (Exception ex)
        {
            await HandleSendError(ex);
        }
    }

    // Crea un ChatMessage con Kind=ERR y lo añade al chat.
    private async Task HandleSendError(Exception ex)
    {
        var errMsg = new ChatMessage(DateTime.Now, "ERR", ex.Message) { Kind = "ERR" };
        await MainThread.InvokeOnMainThreadAsync(() => _chat.Add(errMsg, ShowSystemMessages));
    }

    // Navega a la página de configuración del ESP32.
    [RelayCommand(CanExecute = nameof(CanNavigate))]
    private async Task NavigateToEsp32ConfigAsync()
    {
        await _navigation.GoToAsync("//esp32Config");
    }

    // Pide confirmación y borra todos los mensajes de SQLite y del chat.
    [RelayCommand]
    private async Task ClearAllChatsAsync()
    {
        var page = Application.Current?.MainPage;
        if (page is null) return;

        var ok = await page.DisplayAlert(
            "Limpiar chats",
            "¿Borrar todos los chats guardados? No se puede deshacer.",
            "Borrar",
            "Cancelar");

        if (!ok) return;

        await _store.ClearAllAsync();
        _chat.Clear();
        Status = "Chats borrados";
    }

    // Indica si el usuario puede navegar a la sub-página de config.
    private bool CanNavigate() => IsConnected;

    // Indica si hay dispositivos emparejados en la lista. Usado para alternar
    // entre la lista y el botón grande "Buscar dispositivos BT".
    // Se invalida al cambiar la coleccion (ver suscripcion en el constructor).
    public bool HasDevices => Devices.Count > 0;
    public bool HasNoDevices => !HasDevices;

    // Convierte un mensaje guardado en SQLite en un ChatMessage con su Kind, Source, IsCommand.
    private static ChatMessage RestoreChatMessage(StoredChatMessage h)
    {
        var kind = h.Direction switch
        {
            "OK" or "SYS" => "OK",
            "ERR" => "ERR",
            "TX" => "TX",
            _ => "RX"
        };
        var source = string.IsNullOrEmpty(h.Source) ? null : h.Source;
        var isCommand = h.Direction == "TX" && h.Text.StartsWith("/");
        return new ChatMessage(h.Timestamp, h.Direction, h.Text)
        {
            Kind = kind,
            Source = source,
            IsCommand = isCommand
        };
    }

    // Construye un StoredChatMessage listo para guardarse en SQLite.
    private StoredChatMessage BuildStoredMessage(string direction, string source, string text, DateTime timestamp)
    {
        return new StoredChatMessage
        {
            Mac = ConnectedMac,
            Direction = direction,
            Source = source,
            Text = text,
            Timestamp = timestamp
        };
    }

    // Implementación explícita de IChatCoordinator para exponer los commands como ICommand.
    ICommand IChatCoordinator.RefreshCommand => RefreshCommand;
    ICommand IChatCoordinator.ConnectCommand => ConnectCommand;
    ICommand IChatCoordinator.SendCommand => SendCommand;
    ICommand IChatCoordinator.DisconnectCommand => DisconnectCommand;
    ICommand IChatCoordinator.ClearAllChatsCommand => ClearAllChatsCommand;
    ICommand IChatCoordinator.NavigateToEsp32ConfigCommand => NavigateToEsp32ConfigCommand;
    bool IChatCoordinator.HasNoDevices => HasNoDevices;
    bool IChatCoordinator.IsRefreshing => IsRefreshing;
}