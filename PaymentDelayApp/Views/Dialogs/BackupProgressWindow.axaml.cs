using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PaymentDelayApp.Views.Dialogs;

public partial class BackupProgressWindow : Window
{
    public BackupProgressWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
