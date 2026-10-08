using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace Cantimplora.Platforms.Android;

// Helper estático para pedir el permiso BLUETOOTH_CONNECT en runtime (Android 12+).
public static class BluetoothPermissionHelper
{
    private const int RequestCode = 9001;
    private static TaskCompletionSource<bool>? _tcs;

    // Pide el permiso si falta. Devuelve true si ya lo teníamos o si el usuario lo concede.
    public static async Task<bool> EnsureAsync()
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity is null)
            return false;

        var needed = new List<string>();

        if (Build.VERSION.SdkInt >= BuildVersionCodes.S &&
            ActivityCompat.CheckSelfPermission(activity, Manifest.Permission.BluetoothConnect) != Permission.Granted)
        {
            needed.Add(Manifest.Permission.BluetoothConnect);
        }

        if (needed.Count == 0)
            return true;

        _tcs = new TaskCompletionSource<bool>();
        ActivityCompat.RequestPermissions(activity, needed.ToArray(), RequestCode);
        return await _tcs.Task;
    }

    // Callback desde MainActivity cuando el sistema responde al diálogo de permiso.
    public static void OnResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        if (requestCode != RequestCode) return;
        var tcs = _tcs;
        _tcs = null;
        if (tcs is null) return;

        var allGranted = grantResults.All(g => g == Permission.Granted);
        tcs.SetResult(allGranted);
    }
}