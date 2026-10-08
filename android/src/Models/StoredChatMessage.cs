using SQLite;

namespace Cantimplora.Models;

// Entidad SQLite de un mensaje guardado. Tabla 'Messages'.
[Table("Messages")]
public class StoredChatMessage
{
    // Identificador autoincremental, usado para ordenar cronológicamente.
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    // MAC del dispositivo BT con el que se intercambió el mensaje. Indexada.
    [Indexed]
    public string Mac { get; set; } = string.Empty;

    // Dirección lógica: "TX", "RX", "OK" o "ERR".
    public string Direction { get; set; } = "RX";

    // Identificador del nodo origen (id LoRa del ESP32, en formato XX).
    public string Source { get; set; } = string.Empty;

    // Texto del mensaje.
    public string Text { get; set; } = string.Empty;

    // Momento de recepción/envío en hora local del móvil.
    public DateTime Timestamp { get; set; }
}