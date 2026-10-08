using Cantimplora.Models;
using SQLite;

namespace Cantimplora.Services;

// Almacén SQLite de mensajes y settings. Persiste el histórico de chats y la configuración.
public class MessageStore : IMessageStore
{
    private readonly string _dbPath;
    private SQLiteAsyncConnection? _conn;

    // Construye la ruta del fichero de BD en AppDataDirectory.
    public MessageStore()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "cantimplora.db3");
    }

    // Crea la conexión async y las tablas si no existen.
    public async Task InitializeAsync()
    {
        _conn ??= new SQLiteAsyncConnection(_dbPath);
        await _conn.CreateTableAsync<StoredChatMessage>();
        await _conn.CreateTableAsync<StoredSetting>();
    }

    // Devuelve hasta N mensajes de la MAC indicada, ordenados por id (cronológico).
    public async Task<IReadOnlyList<StoredChatMessage>> LoadAsync(string mac, int max = 500)
    {
        if (_conn is null) return Array.Empty<StoredChatMessage>();
        var rows = await _conn.Table<StoredChatMessage>()
            .Where(m => m.Mac == mac)
            .OrderBy(m => m.Id)
            .Take(max)
            .ToListAsync();
        return rows;
    }

    // Inserta un mensaje en la tabla.
    public async Task SaveAsync(StoredChatMessage message)
    {
        if (_conn is null) return;
        await _conn.InsertAsync(message);
    }

    // Borra todos los mensajes de todas las MACs.
    public async Task ClearAllAsync()
    {
        if (_conn is null) return;
        await _conn.DeleteAllAsync<StoredChatMessage>();
    }

    // Devuelve el valor guardado de una setting por clave, o null si no existe.
    public async Task<string?> GetSettingAsync(string key)
    {
        if (_conn is null) return null;
        var row = await _conn.Table<StoredSetting>().Where(s => s.Key == key).FirstOrDefaultAsync();
        return row?.Value;
    }

    // Guarda el valor de una setting (upsert).
    public async Task SetSettingAsync(string key, string value)
    {
        if (_conn is null) return;
        var existing = await _conn.Table<StoredSetting>().Where(s => s.Key == key).FirstOrDefaultAsync();
        if (existing is null)
            await _conn.InsertAsync(new StoredSetting { Key = key, Value = value });
        else
        {
            existing.Value = value;
            await _conn.UpdateAsync(existing);
        }
    }
}