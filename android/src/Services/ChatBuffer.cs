using System.Collections.ObjectModel;
using Cantimplora.Models;
using Cantimplora.ViewModels;

namespace Cantimplora.Services;

// Encapsula la lista completa de mensajes (_allMessages) y la lista visible (Messages).
// Todas las mutaciones del chat pasan por aquí para mantener ambas listas coherentes.
// No guarda el flag de ShowSystemMessages: el caller lo pasa en cada operación.
public class ChatBuffer
{
    private readonly ObservableCollection<ChatMessage> _visible;
    private readonly ChatFilter _filter;
    private readonly List<ChatMessage> _all = new();

    public ChatBuffer(ObservableCollection<ChatMessage> visible, ChatFilter filter)
    {
        _visible = visible;
        _filter = filter;
    }

    // Vista de solo lectura de todos los mensajes (incluidos los ocultos).
    public IReadOnlyList<ChatMessage> All => _all;

    // Añade un mensaje al buffer. Si pasa el filtro y showSystem lo permite, también a la lista visible.
    public void Add(ChatMessage msg, bool showSystem)
    {
        _all.Add(msg);
        if (_filter.ShouldBeVisible(msg.Kind, msg.IsCommand, showSystem))
            _visible.Add(msg);
    }

    // Limpia la lista completa y la visible.
    public void Clear()
    {
        _all.Clear();
        _visible.Clear();
    }

    // Vacía la lista visible y la reconstruye desde _all aplicando el filtro con el flag actual.
    public void Rebuild(bool showSystem)
    {
        _visible.Clear();
        foreach (var m in _all)
        {
            if (_filter.ShouldBeVisible(m.Kind, m.IsCommand, showSystem))
                _visible.Add(m);
        }
    }

    // Carga mensajes desde un histórico de SQLite. Limpia el buffer y aplica el filtro.
    // El caller pasa la función de conversión StoredChatMessage → ChatMessage.
    public void LoadFrom(IEnumerable<StoredChatMessage> history, Func<StoredChatMessage, ChatMessage> restore, bool showSystem)
    {
        _all.Clear();
        _visible.Clear();
        foreach (var h in history)
        {
            var msg = restore(h);
            _all.Add(msg);
            if (_filter.ShouldBeVisible(msg.Kind, msg.IsCommand, showSystem))
                _visible.Add(msg);
        }
    }
}