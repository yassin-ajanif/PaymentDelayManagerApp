using Avalonia.Markup.Xaml;

namespace PaymentDelayApp.Views.Dialogs;

public partial class ReglementConfirmWindow : Avalonia.Controls.Window
{
    public ReglementConfirmWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
