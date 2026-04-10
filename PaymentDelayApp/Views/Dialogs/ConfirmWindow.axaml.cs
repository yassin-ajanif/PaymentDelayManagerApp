using Avalonia.Markup.Xaml;

namespace PaymentDelayApp.Views.Dialogs;

public partial class ConfirmWindow : Avalonia.Controls.Window
{
    public ConfirmWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
