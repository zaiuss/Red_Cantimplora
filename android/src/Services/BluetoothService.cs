using Android;
using Android.Bluetooth;
using BTDevice = Cantimplora.Models.BluetoothDevice;
using System.IO;
using System.Text;
using System.Threading;

namespace Cantimplora.Services;

public class BluetoothService : IBluetoothService
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReadPollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(30);

    private readonly BluetoothAdapter? _adapter = BluetoothAdapter.DefaultAdapter;

    private static readonly Java.Util.UUID SppUuid =
        Java.Util.UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");

    private BluetoothSocket? _socket;
    private Stream? _input;
    private Stream? _output;
    private CancellationTokenSource? _cts;

    // Indica si el adaptador BT del sistema está encendido.
    public bool IsEnabled => _adapter?.IsEnabled ?? false;

    // Estado de la conexión SPP actual.
    public bool IsConnected { get; private set; }

    // Se dispara al cambiar el estado de la conexión.
    public event EventHandler<bool>? ConnectionChanged;

    // Se dispara por cada línea completa leída del socket.
    public event EventHandler<string>? MessageReceived;

    // Devuelve la lista de dispositivos emparejados del sistema.
    public System.Collections.Generic.IReadOnlyList<BTDevice> GetBondedDevices()
    {
        var list = new System.Collections.Generic.List<BTDevice>();
        if (_adapter is null || !_adapter.IsEnabled) return list;

        foreach (var dev in _adapter.BondedDevices ?? new System.Collections.Generic.List<Android.Bluetooth.BluetoothDevice>())
        {
            var mac = dev.Address ?? string.Empty;
            list.Add(new BTDevice(dev.Name ?? "Desconocido", mac));
        }
        return list;
    }

    // Abre un socket SPP, conecta con timeout, arranca el read loop.
    public async Task ConnectAsync(string macAddress)
    {
        if (_adapter is null || !_adapter.IsEnabled)
            throw new InvalidOperationException("Bluetooth desactivado");

        var dev = _adapter.GetRemoteDevice(macAddress)
            ?? throw new InvalidOperationException($"No se encuentra {macAddress}");

        try { Disconnect(); } catch { }

        _socket = dev.CreateRfcommSocketToServiceRecord(SppUuid);

        await Task.Run(() => _socket.Connect()).WaitAsync(ConnectTimeout);

        _input = _socket.InputStream;
        _output = _socket.OutputStream;
        _cts = new CancellationTokenSource();

        IsConnected = true;
        ConnectionChanged?.Invoke(this, true);

        _ = ReadLoopAsync(_cts.Token);
    }

    // Bucle principal: lee bytes del socket, extrae líneas terminadas en \n, dispara MessageReceived.
    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[1024];
        var sb = new System.Text.StringBuilder();
        var lastDataUtc = DateTime.UtcNow;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var n = await ReadChunkAsync(buffer, ct);
                if (n < 0) break;
                ProcessChunk(buffer, n, sb, ref lastDataUtc);
            }
        }
        catch (System.OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageReceived?.Invoke(this, $"[ERR] {ex.GetType().Name}: {ex.Message}");
            MarkDisconnected();
        }
    }

    // Lee bytes del socket. Devuelve el número leído, o -1 si se canceló o cerró.
    private async Task<int> ReadChunkAsync(byte[] buffer, CancellationToken ct)
    {
        try
        {
            return await _input!.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
        }
        catch (System.OperationCanceledException) { return -1; }
        catch (System.IO.IOException) { return -1; }
    }

    // Procesa un chunk: decodifica, acumula en el buffer parcial y emite cada línea completa.
    // Si no llegan datos y vence el watchdog, emite el mensaje de reset.
    private void ProcessChunk(byte[] buffer, int n, StringBuilder sb, ref DateTime lastDataUtc)
    {
        if (n > 0)
        {
            lastDataUtc = DateTime.UtcNow;
            var chunk = System.Text.Encoding.UTF8.GetString(buffer, 0, n);
            sb.Append(chunk);
            EmitCompleteLines(sb);
        }
        else if (sb.Length > 0 && (DateTime.UtcNow - lastDataUtc) > WatchdogInterval)
        {
            var dropped = sb.Length;
            sb.Clear();
            lastDataUtc = DateTime.UtcNow;
            MessageReceived?.Invoke(this, $"[SYS] watchdog: reset de buffer parcial ({dropped} bytes)");
        }
    }

    // Extrae todas las líneas completas del buffer parcial y las emite por MessageReceived.
    private void EmitCompleteLines(StringBuilder sb)
    {
        int idx;
        while ((idx = sb.ToString().IndexOf('\n')) >= 0)
        {
            var line = sb.ToString(0, idx).TrimEnd('\r');
            sb.Remove(0, idx + 1);
            if (line.Length > 0)
                MessageReceived?.Invoke(this, line);
        }
    }

    // Añade \n si falta y escribe el texto en UTF-8 por el socket.
    public async Task SendAsync(string text)
    {
        if (_output is null)
            throw new InvalidOperationException("No conectado");

        if (string.IsNullOrEmpty(text)) return;
        var line = text.EndsWith("\n") ? text : text + "\n";

        var bytes = System.Text.Encoding.UTF8.GetBytes(line);
        await Task.Run(() => _output.Write(bytes, 0, bytes.Length));
    }

    // Cierra el socket limpiamente, cancela el read loop y marca como desconectado.
    public void Disconnect()
    {
        try { _cts?.Cancel(); } catch { }
        try { _socket?.Close(); } catch { }

        _socket = null;
        _input = null;
        _output = null;
        _cts = null;

        if (IsConnected) MarkDisconnected();
    }

    // Actualiza IsConnected y dispara ConnectionChanged(false).
    private void MarkDisconnected()
    {
        IsConnected = false;
        ConnectionChanged?.Invoke(this, false);
    }
}