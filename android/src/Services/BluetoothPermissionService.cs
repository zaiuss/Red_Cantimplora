namespace Cantimplora.Services;

// Implementación de IPermissionService que delega al helper Android de BluetoothPermissionHelper.
public class BluetoothPermissionService : IPermissionService
{
    public Task<bool> EnsureBluetoothAsync()
    {
        return Cantimplora.Platforms.Android.BluetoothPermissionHelper.EnsureAsync();
    }
}