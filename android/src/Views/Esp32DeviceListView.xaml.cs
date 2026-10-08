using System.Collections.ObjectModel;
using System.Windows.Input;
using Cantimplora.Models;

namespace Cantimplora.Views;

// Lista de dispositivos BT emparejados con estado vacío.
// Muestra un label "Refresca para buscar dispositivos" cuando no hay
// dispositivos, o la lista de BtDeviceItemView cuando los hay.
// Visibilidad: controlada por el padre (no expone BindableProperty IsVisible).
public partial class Esp32DeviceListView : ContentView
{
    public static readonly BindableProperty DevicesProperty =
        BindableProperty.Create(nameof(Devices), typeof(ObservableCollection<BluetoothDevice>), typeof(Esp32DeviceListView));

    public static readonly BindableProperty HasDevicesProperty =
        BindableProperty.Create(nameof(HasDevices), typeof(bool), typeof(Esp32DeviceListView),
            defaultValue: false);

    public static readonly BindableProperty HasNoDevicesProperty =
        BindableProperty.Create(nameof(HasNoDevices), typeof(bool), typeof(Esp32DeviceListView),
            defaultValue: true);

    public static readonly BindableProperty ConnectCommandProperty =
        BindableProperty.Create(nameof(ConnectCommand), typeof(ICommand), typeof(Esp32DeviceListView));

    public ObservableCollection<BluetoothDevice> Devices
    {
        get => (ObservableCollection<BluetoothDevice>)GetValue(DevicesProperty);
        set => SetValue(DevicesProperty, value);
    }

    public bool HasDevices
    {
        get => (bool)GetValue(HasDevicesProperty);
        set => SetValue(HasDevicesProperty, value);
    }

    public bool HasNoDevices
    {
        get => (bool)GetValue(HasNoDevicesProperty);
        set => SetValue(HasNoDevicesProperty, value);
    }

    public ICommand ConnectCommand
    {
        get => (ICommand)GetValue(ConnectCommandProperty);
        set => SetValue(ConnectCommandProperty, value);
    }

    public Esp32DeviceListView()
    {
        InitializeComponent();
    }
}
