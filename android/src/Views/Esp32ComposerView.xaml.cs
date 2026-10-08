using System.Windows.Input;

namespace Cantimplora.Views;

// Composer del chat: Entry + botón "Enviar".
// BindableProperty:
//   - Text: texto del Entry (TwoWay).
//   - SendCommand: comando que se ejecuta al pulsar "Enviar" o al pulsar Enter.
// Visibilidad: controlada por el padre (no expone BindableProperty IsVisible).
public partial class Esp32ComposerView : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(Esp32ComposerView),
            defaultValue: string.Empty);

    public static readonly BindableProperty SendCommandProperty =
        BindableProperty.Create(nameof(SendCommand), typeof(ICommand), typeof(Esp32ComposerView));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ICommand SendCommand
    {
        get => (ICommand)GetValue(SendCommandProperty);
        set => SetValue(SendCommandProperty, value);
    }

    public Esp32ComposerView()
    {
        InitializeComponent();
    }
}
