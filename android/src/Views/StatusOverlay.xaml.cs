namespace Cantimplora.Views;

// Overlay generico: muestra un mensaje centrado sobre fondo semitransparente.
// Configurable via Text e IsVisible (BindableProperties).
// Uso: <views:StatusOverlay Text="..." IsVisible="..." />.
public partial class StatusOverlay : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(StatusOverlay),
            defaultValue: string.Empty);

    public static readonly BindableProperty IsVisibleProperty =
        BindableProperty.Create(nameof(IsVisible), typeof(bool), typeof(StatusOverlay),
            defaultValue: false);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsVisible
    {
        get => (bool)GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    public StatusOverlay()
    {
        InitializeComponent();
    }
}