using CommunityToolkit.Mvvm.ComponentModel;

namespace Cantimplora.Models;

// Modelo de un dispositivo BT emparejado.
public partial class BluetoothDevice : ObservableObject
{
    // Nombre legible del dispositivo (ej. "ESP32-Lora-1").
    [ObservableProperty]
    private string name = string.Empty;

    // Dirección MAC del dispositivo (formato AA:BB:CC:11:22:33).
    [ObservableProperty]
    private string macAddress = string.Empty;

    public BluetoothDevice() { }

    public BluetoothDevice(string name, string macAddress)
    {
        Name = name;
        MacAddress = macAddress;
    }

    // Cadena legible: nombre + MAC, o solo MAC si no hay nombre.
    public override string ToString() => string.IsNullOrWhiteSpace(Name)
        ? MacAddress
        : $"{Name} ({MacAddress})";
}