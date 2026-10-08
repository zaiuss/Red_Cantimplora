namespace Cantimplora.Services;

// Implementación Android de IToastService usando Android.Widget.Toast.
public class ToastService : IToastService
{
    public void ShowShort(string message)
    {
        Android.Widget.Toast.MakeText(Android.App.Application.Context, message, Android.Widget.ToastLength.Short)?.Show();
    }
}