using System.Windows.Input;

namespace Cantimplora.Views;

// Item de la lista de dispositivos BT emparejados.
// BindingContext esperado: BluetoothDevice (Name, MacAddress).
// BindableProperty ConnectCommand: el comando a ejecutar al pulsar "Conectar".
public partial class BtDeviceItemView : ContentView
{
    public static readonly BindableProperty ConnectCommandProperty =
        BindableProperty.Create(nameof(ConnectCommand), typeof(ICommand), typeof(BtDeviceItemView));

    public ICommand ConnectCommand
    {
        get => (ICommand)GetValue(ConnectCommandProperty);
        set => SetValue(ConnectCommandProperty, value);
    }

    public BtDeviceItemView()
    {
        InitializeComponent();
    }
}