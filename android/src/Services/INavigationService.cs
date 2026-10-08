namespace Cantimplora.Services;

// Encapsula la navegación entre páginas (Shell.GoToAsync).
public interface INavigationService
{
    Task GoToAsync(string route);
}