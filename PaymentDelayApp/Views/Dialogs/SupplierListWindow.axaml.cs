using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using PaymentDelayApp.Models;
using PaymentDelayApp.ViewModels;

namespace PaymentDelayApp.Views.Dialogs;

public partial class SupplierListWindow : Window
{
    public SupplierListWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void SupplierGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not DataGrid grid || grid.DataContext is not SupplierListViewModel vm)
            return;
        if (e.Source is not Visual source)
            return;
        var row = source.FindAncestorOfType<DataGridRow>();
        if (row?.DataContext is not SupplierListRow supplierRow)
            return;
        vm.SelectedSupplierRow = supplierRow;
        vm.EditSupplierCommand.Execute(null);
    }
}
