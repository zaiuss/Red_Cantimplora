namespace Cantimplora.Services;

// Parsea la respuesta de /stat: "OK id=XX seq=N mesh=ON buf=N/M bt=ON tx=A/Bms".
// Extrae id (cadena), tx (repeticiones) y gap (ms entre repeticiones).
public class Esp32StatParser
{
    // Intenta parsear la línea y extraer id, tx y gap. Devuelve true si encuentra id= o tx=.
    // Si id no aparece, devuelve string.Empty.
    // Si tx o gap no aparecen o no parsean, devuelve 0.
    public bool TryParseStatLine(string line, out string id, out int tx, out int gap)
    {
        id = string.Empty;
        tx = 0;
        gap = 0;
        var foundAny = false;

        foreach (var token in line.Split(' '))
        {
            if (token.StartsWith("id=", StringComparison.Ordinal))
            {
                id = token.Substring(3);
                foundAny = true;
            }
            else if (token.StartsWith("tx=", StringComparison.Ordinal))
            {
                var value = token.Substring(3);
                var slashIdx = value.IndexOf('/');
                if (slashIdx > 0)
                {
                    if (int.TryParse(value.Substring(0, slashIdx), out var parsedTx))
                        tx = parsedTx;
                    var gapStr = value.Substring(slashIdx + 1).TrimEnd('m', 's');
                    if (int.TryParse(gapStr, out var parsedGap))
                        gap = parsedGap;
                }
                foundAny = true;
            }
        }

        return foundAny;
    }
}