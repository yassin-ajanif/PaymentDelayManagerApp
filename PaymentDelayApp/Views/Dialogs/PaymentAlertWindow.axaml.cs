using Avalonia.Markup.Xaml;

namespace PaymentDelayApp.Views.Dialogs;

public partial class PaymentAlertWindow : Avalonia.Controls.Window
{
    public PaymentAlertWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
