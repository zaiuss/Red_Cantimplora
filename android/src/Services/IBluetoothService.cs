using Cantimplora.Models;

namespace Cantimplora.Services;

// Contrato del servicio BT: estado, eventos, lista de emparejados, conexión y envío.
public interface IBluetoothService
{
    // Indica si el adaptador BT del sistema está encendido.
    bool IsEnabled { get; }

    // Estado de la conexión SPP actual.
    bool IsConnected { get; }

    // Se dispara al cambiar el estado de la conexión.
    event EventHandler<bool>? ConnectionChanged;

    // Se dispara por cada línea completa leída del socket.
    event EventHandler<string>? MessageReceived;

    // Devuelve la lista de dispositivos emparejados del sistema.
    IReadOnlyList<BluetoothDevice> GetBondedDevices();

    // Abre un socket SPP y conecta con el dispositivo indicado.
    Task ConnectAsync(string macAddress);

    // Envía un texto (terminado en \n) por la conexión activa.
    Task SendAsync(string text);

    // Cierra la conexión SPP y libera recursos.
    void Disconnect();
}