using System.Collections.ObjectModel;
using System.Windows.Input;
using Cantimplora.Models;
using Cantimplora.ViewModels;

namespace Cantimplora.Services;

// Contrato que la página principal (Esp32Page) consume del Esp32ViewModel.
// Define qué propiedades y comandos están visibles para esa página.
public interface IChatCoordinator
{
    ObservableCollection<BluetoothDevice> Devices { get; }
    ObservableCollection<ChatMessage> Messages { get; }

    string Status { get; }
    bool IsConnected { get; }
    bool IsNotConnected { get; }
    bool IsBusy { get; }
    bool IsDisconnecting { get; }
    string ConnectedName { get; }
    string ConnectedMac { get; }
    string Esp32Id { get; }
    string DraftMessage { get; set; }

    bool HasNoDevices { get; }
    bool HasDevices { get; }
    bool IsRefreshing { get; }

    ICommand RefreshCommand { get; }
    ICommand ConnectCommand { get; }
    ICommand SendCommand { get; }
    ICommand DisconnectCommand { get; }
    ICommand ClearAllChatsCommand { get; }
    ICommand NavigateToEsp32ConfigCommand { get; }

    event EventHandler<string>? InfoMessage;
}