namespace Cantimplora.Views;

// Burbuja de un mensaje del chat.
// BindingContext esperado: ChatMessage (Kind, Header, Text, Timestamp, IsCommand).
public partial class ChatBubbleView : ContentView
{
    public ChatBubbleView()
    {
        InitializeComponent();
    }
}