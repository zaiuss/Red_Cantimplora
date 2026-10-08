using System.Windows.Input;

namespace Cantimplora.Views;

// Bloque de "conexión inactiva" de la página principal:
//   - Etiqueta "Desconectado" a la izquierda.
//   - Botón "⟳" a la derecha que ejecuta el RefreshCommand del VM.
// Visibilidad: controlada por el padre (no expone BindableProperty IsVisible).
public partial class InactiveLinkView : ContentView
{
    public static readonly BindableProperty RefreshCommandProperty =
        BindableProperty.Create(nameof(RefreshCommand), typeof(ICommand), typeof(InactiveLinkView));

    public ICommand RefreshCommand
    {
        get => (ICommand)GetValue(RefreshCommandProperty);
        set => SetValue(RefreshCommandProperty, value);
    }

    public InactiveLinkView()
    {
        InitializeComponent();
    }
}
