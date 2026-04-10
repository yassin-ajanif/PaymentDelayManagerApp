using Avalonia.Markup.Xaml;

namespace PaymentDelayApp.Views.Dialogs;

public partial class InvoiceEditWindow : Avalonia.Controls.Window
{
    public InvoiceEditWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
