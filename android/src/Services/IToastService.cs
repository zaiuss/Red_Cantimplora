namespace Cantimplora.Services;

// Encapsula la muestra de toasts en la UI.
public interface IToastService
{
    void ShowShort(string message);
}