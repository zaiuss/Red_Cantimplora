namespace Cantimplora.Services;

// Encapsula la petición de permisos (Bluetooth, etc.).
public interface IPermissionService
{
    // Pide el permiso BT si falta. Devuelve true si ya lo teníamos o si el usuario lo concede.
    Task<bool> EnsureBluetoothAsync();
}