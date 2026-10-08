namespace Cantimplora.Views;

// Bloque de "conexión activa" de la página principal:
//   - Texto: "Conectado a X" + MAC + "NODO_XX".
//   - Switch on/off a la derecha que re-emite el evento Toggled al padre.
// Visibilidad: controlada por el padre (no expone BindableProperty IsVisible).
public partial class ActiveLinkView : ContentView
{
    public static readonly BindableProperty ConnectedNameProperty =
        BindableProperty.Create(nameof(ConnectedName), typeof(string), typeof(ActiveLinkView),
            defaultValue: string.Empty);

    public static readonly BindableProperty MacAddressProperty =
        BindableProperty.Create(nameof(MacAddress), typeof(string), typeof(ActiveLinkView),
            defaultValue: string.Empty);

    public static readonly BindableProperty Esp32IdProperty =
        BindableProperty.Create(nameof(Esp32Id), typeof(string), typeof(ActiveLinkView),
            defaultValue: string.Empty);

    public static readonly BindableProperty IsConnectedProperty =
        BindableProperty.Create(nameof(IsConnected), typeof(bool), typeof(ActiveLinkView),
            defaultValue: false);

    public string ConnectedName
    {
        get => (string)GetValue(ConnectedNameProperty);
        set => SetValue(ConnectedNameProperty, value);
    }

    public string MacAddress
    {
        get => (string)GetValue(MacAddressProperty);
        set => SetValue(MacAddressProperty, value);
    }

    public string Esp32Id
    {
        get => (string)GetValue(Esp32IdProperty);
        set => SetValue(Esp32IdProperty, value);
    }

    public bool IsConnected
    {
        get => (bool)GetValue(IsConnectedProperty);
        set => SetValue(IsConnectedProperty, value);
    }

    // Re-emite el Toggled del Switch interno. El padre lo usa para pedir desconexión.
    public event EventHandler<ToggledEventArgs>? Toggled;

    public ActiveLinkView()
    {
        InitializeComponent();
    }

    // Handler interno: re-emite el evento hacia el padre.
    private void OnSwitchToggled(object? sender, ToggledEventArgs e)
    {
        Toggled?.Invoke(this, e);
    }
}
