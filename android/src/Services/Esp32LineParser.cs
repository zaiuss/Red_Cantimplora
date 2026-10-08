using Cantimplora.ViewModels;

namespace Cantimplora.Services;

// Parsea líneas individuales del ESP32 (RX, OK, ERR) en ChatMessage.
// Stateless: una instancia puede usarse para parsear múltiples líneas.
public class Esp32LineParser
{
    // Parsea una línea del ESP32 y devuelve un ChatMessage con su Kind y Source.
    // Si la línea empieza por "RX src=" → Kind="RX", Source=src, Text=payload (si lo hay).
    // Si empieza por "OK " → Kind="OK".
    // Si empieza por "ERR " → Kind="ERR".
    // En cualquier otro caso → Kind="RX", Text=line.
    public ChatMessage Parse(string line)
    {
        if (TryParseRxLine(line, out var src, out var payload, out var rxOk))
            return new ChatMessage(DateTime.Now, "RX", rxOk ? payload : line) { Kind = "RX", Source = src };

        if (TryParseOkLine(line, out var okText))
            return new ChatMessage(DateTime.Now, "OK", okText) { Kind = "OK" };

        if (TryParseErrLine(line, out var errText))
            return new ChatMessage(DateTime.Now, "ERR", errText) { Kind = "ERR" };

        return new ChatMessage(DateTime.Now, "RX", line) { Kind = "RX" };
    }

    // Intenta parsear una línea "RX src=XX ... payload="...". Devuelve true si es RX.
    // Si no encuentra payload entrecomillado, payloadOk=false y el caller usa line como Text.
    public bool TryParseRxLine(string line, out string src, out string payload, out bool payloadOk)
    {
        const string rxPrefix = "RX src=";
        src = string.Empty;
        payload = string.Empty;
        payloadOk = false;

        if (!line.StartsWith(rxPrefix, StringComparison.Ordinal))
            return false;

        var srcStart = rxPrefix.Length;
        var srcEnd = line.IndexOf(' ', srcStart);
        src = srcEnd > srcStart ? line.Substring(srcStart, srcEnd - srcStart) : string.Empty;

        const string payloadKey = "payload=\"";
        var idx = line.IndexOf(payloadKey, StringComparison.Ordinal);
        if (idx < 0) return true;

        var start = idx + payloadKey.Length;
        var end = line.IndexOf('"', start);
        if (end <= start) return true;

        payload = line.Substring(start, end - start);
        payloadOk = true;
        return true;
    }

    // Intenta parsear una línea que empieza por "OK ". Devuelve true y el texto si es OK.
    public bool TryParseOkLine(string line, out string text)
    {
        const string prefix = "OK ";
        if (line.StartsWith(prefix, StringComparison.Ordinal))
        {
            text = line;
            return true;
        }
        text = string.Empty;
        return false;
    }

    // Intenta parsear una línea que empieza por "ERR ". Devuelve true y el texto si es ERR.
    public bool TryParseErrLine(string line, out string text)
    {
        const string prefix = "ERR ";
        if (line.StartsWith(prefix, StringComparison.Ordinal))
        {
            text = line;
            return true;
        }
        text = string.Empty;
        return false;
    }
}