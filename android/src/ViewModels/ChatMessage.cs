using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Cantimplora.ViewModels;

// Modelo in-memory de un mensaje del chat. Usado por el CollectionView.
public class ChatMessage : INotifyPropertyChanged
{
    public ChatMessage(DateTime timestamp, string direction, string text)
    {
        Timestamp = timestamp;
        Direction = direction;
        Text = text;
    }

    public DateTime Timestamp { get; init; }
    public string Direction { get; init; } = "RX";
    public string Text { get; init; } = string.Empty;

    // Categoría visual: "RX", "TX", "OK" o "ERR". Controla colores y alineación en XAML.
    public string Kind { get; init; } = "RX";

    // Identificador del nodo origen (id LoRa). Null para mensajes del sistema (OK/ERR).
    public string? Source { get; init; }

    // Si true, es un comando enviado (texto empieza por /).
    public bool IsCommand { get; init; }

    // Texto que se muestra arriba del mensaje. "NODO_XX (Rx/Tx)" o el Direction si no hay Source.
    public string Header
    {
        get
        {
            if (Source is null) return Direction;
            var suffix = Direction == "TX" ? "Tx" : "Rx";
            return $"NODO_{Source} ({suffix})";
        }
    }

    // Cadena completa con timestamp, header y texto (para debug).
    public string Display => $"[{Timestamp:HH:mm:ss}] {Header}  {Text}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}