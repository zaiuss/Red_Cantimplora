using Cantimplora.Models;

namespace Cantimplora.Services;

// Contrato del almacén persistente de mensajes y settings.
public interface IMessageStore
{
    // Crea la conexión y las tablas si no existen.
    Task InitializeAsync();

    // Devuelve hasta N mensajes de la MAC indicada, ordenados por id (cronológico).
    Task<IReadOnlyList<StoredChatMessage>> LoadAsync(string mac, int max = 500);

    // Inserta un mensaje en la tabla.
    Task SaveAsync(StoredChatMessage message);

    // Borra todos los mensajes de todas las MACs.
    Task ClearAllAsync();

    // Devuelve el valor guardado de una setting por clave, o null si no existe.
    Task<string?> GetSettingAsync(string key);

    // Guarda el valor de una setting (upsert).
    Task SetSettingAsync(string key, string value);
}