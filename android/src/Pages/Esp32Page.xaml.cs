using Cantimplora.Services;

namespace Cantimplora.Pages;

// Code-behind de la página principal (BT Connect + chat).
public partial class Esp32Page : ContentPage
{
    // servicio de toasts (avisos efímeros)
    private readonly IToastService _toast;

    // Inyecta el coordinador de chat (interfaz) y el servicio de toasts, y suscribe al evento de cambio de tamaño del chat y al de InfoMessage.
    public Esp32Page(IChatCoordinator vm, IToastService toast)
    {
        InitializeComponent();
        BindingContext = vm;
        _toast = toast;
        // scroll al fondo del chat cuando cambia el tamaño (p. ej. al subir el teclado)
        ChatCollectionView.SizeChanged += OnChatCollectionViewSizeChanged;
        // evento del VM: lo consumo en OnInfoMessage para mostrar toasts
        vm.InfoMessage += OnInfoMessage;

        // overlays
        RefreshingOverlay.SetBinding(Views.StatusOverlay.IsVisibleProperty,
            new Binding(nameof(IChatCoordinator.IsRefreshing), source: vm));
        DisconnectOverlay.SetBinding(Views.StatusOverlay.IsVisibleProperty,
            new Binding(nameof(IChatCoordinator.IsDisconnecting), source: vm));

        // cabecera
        Link.SetBinding(Views.ActiveLinkView.ConnectedNameProperty,
            new Binding(nameof(IChatCoordinator.ConnectedName), source: vm));
        Link.SetBinding(Views.ActiveLinkView.MacAddressProperty,
            new Binding(nameof(IChatCoordinator.ConnectedMac), source: vm));
        Link.SetBinding(Views.ActiveLinkView.Esp32IdProperty,
            new Binding(nameof(IChatCoordinator.Esp32Id), source: vm));
        Link.SetBinding(Views.ActiveLinkView.IsConnectedProperty,
            new Binding(nameof(IChatCoordinator.IsConnected), source: vm));
        Link.Toggled += OnLinkToggled;

        // cabecera inactiva
        InactiveLink.SetBinding(Views.InactiveLinkView.RefreshCommandProperty,
            new Binding(nameof(IChatCoordinator.RefreshCommand), source: vm));

        // lista de dispositivos
        DeviceList.SetBinding(Views.Esp32DeviceListView.DevicesProperty,
            new Binding(nameof(IChatCoordinator.Devices), source: vm));
        DeviceList.SetBinding(Views.Esp32DeviceListView.HasDevicesProperty,
            new Binding(nameof(IChatCoordinator.HasDevices), source: vm));
        DeviceList.SetBinding(Views.Esp32DeviceListView.HasNoDevicesProperty,
            new Binding(nameof(IChatCoordinator.HasNoDevices), source: vm));
        DeviceList.SetBinding(Views.Esp32DeviceListView.ConnectCommandProperty,
            new Binding(nameof(IChatCoordinator.ConnectCommand), source: vm));

        // composer (Text es TwoWay para que el Entry propague al VM)
        Composer.SetBinding(Views.Esp32ComposerView.TextProperty,
            new Binding(nameof(IChatCoordinator.DraftMessage), source: vm, mode: BindingMode.TwoWay));
        Composer.SetBinding(Views.Esp32ComposerView.SendCommandProperty,
            new Binding(nameof(IChatCoordinator.SendCommand), source: vm));
    }

    // Muestra un toast con el mensaje recibido desde el VM.
    private void OnInfoMessage(object? sender, string message)
    {
        _toast.ShowShort(message);
    }

    // El Switch del ActiveLinkView re-emite su Toggled aquí.
    // La guarda descarta:
    //   - Toggled(true): no es una petición de desconexión.
    //   - Toggled(false) sin conexión activa: no hay nada que desconectar.
    // El bug que nos llevó aquí era la inicialización del binding TwoWay del
    // IsToggled (ya arreglado con Mode=OneWay en ActiveLinkView.xaml). Esta
    // guarda queda como defensa ante Toggled espurios que MAUI/Android puedan
    // disparar en ciclos de IsEnabled/IsVisible.
    private void OnLinkToggled(object? sender, ToggledEventArgs e)
    {
        if (!e.Value && BindingContext is IChatCoordinator vm && vm.IsConnected)
        {
            vm.DisconnectCommand.Execute(null);
        }
    }

    // Al cambiar el tamaño del chat (p. ej. al subir el teclado), hace scroll al fondo.
    private void OnChatCollectionViewSizeChanged(object? sender, EventArgs e)
    {
        // sin conexión no hay chat visible, salimos
        if (BindingContext is not IChatCoordinator vm || !vm.IsConnected) return;
        var source = ChatCollectionView.ItemsSource as System.Collections.IList;
        if (source is null || source.Count == 0) return;

        var last = source[^1];
        // delay para que el layout termine antes de hacer ScrollTo
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
        {
            try
            {
                ChatCollectionView.ScrollTo(last, position: ScrollToPosition.End, animate: false);
            }
            catch
            {
                // catch vacío: ScrollTo puede lanzar si el item ya no está en la colección.
            }
        });
    }

    // Muestra el ActionSheet con las opciones ESP32 Config y Clear DB.
    private async void OnMenuClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not IChatCoordinator vm) return;

        // Diccionario etiqueta→command: evita un switch con strings duplicados y mantiene la lista de opciones cerca del dispatch.
        var options = new Dictionary<string, System.Windows.Input.ICommand>
        {
            ["ESP32 Config"] = vm.NavigateToEsp32ConfigCommand,
            ["Clear DB"] = vm.ClearAllChatsCommand,
        };

        var choice = await DisplayActionSheet("Opciones", "Cancelar", null, options.Keys.ToArray());

        // null = usuario pulsó Cancelar
        if (choice is null) return;
        if (options.TryGetValue(choice, out var cmd) && cmd.CanExecute(null))
            cmd.Execute(null);
    }
}