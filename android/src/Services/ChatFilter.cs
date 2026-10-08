namespace Cantimplora.Services;

// Regla pura de visibilidad de mensajes del chat según el flag de debug.
// Sin estado: el caller pasa el flag en cada llamada.
public class ChatFilter
{
    // Devuelve true si un mensaje del tipo kind y de marca isCommand debe mostrarse
    // dado el estado actual de ShowSystemMessages.
    //   - Si ShowSystemMessages=true, todo es visible.
    //   - Si false: OK, ERR y TX-comando (empieza por /) están ocultos.
    public bool ShouldBeVisible(string kind, bool isCommand, bool showSystem)
    {
        return showSystem
            || (kind != "OK" && kind != "ERR" && !(kind == "TX" && isCommand));
    }
}