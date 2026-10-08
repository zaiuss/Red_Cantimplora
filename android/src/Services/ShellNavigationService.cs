using Cantimplora.Pages;

namespace Cantimplora.Services;

// Implementación de INavigationService usando Shell.Current.
public class ShellNavigationService : INavigationService
{
    public Task GoToAsync(string route)
    {
        return Shell.Current.GoToAsync(route);
    }
}