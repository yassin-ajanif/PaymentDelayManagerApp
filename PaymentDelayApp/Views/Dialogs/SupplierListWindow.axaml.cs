using Avalonia.Markup.Xaml;

namespace PaymentDelayApp.Views.Dialogs;

public partial class SupplierListWindow : Avalonia.Controls.Window
{
    public SupplierListWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
