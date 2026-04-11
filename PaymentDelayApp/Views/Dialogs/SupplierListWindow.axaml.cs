using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
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

    private void SupplierGrid_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Right)
            return;
        if (sender is not DataGrid grid || grid.DataContext is not SupplierListViewModel vm)
            return;
        if (e.Source is not Visual source)
            return;

        var row = source.FindAncestorOfType<DataGridRow>();
        if (row?.DataContext is not SupplierListRow supplierRow)
            return;

        vm.SelectedSupplierRow = supplierRow;

        var delete = new MenuItem
        {
            Header = RowContextHeader("🗑", "Supprimer"),
            Command = vm.DeleteSupplierCommand,
            CommandParameter = supplierRow,
        };

        var menu = new ContextMenu();
        menu.Items.Add(delete);
        menu.Open(row);
    }

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

    private static StackPanel RowContextHeader(string icon, string label) =>
        new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = icon, FontSize = 14, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center },
            },
        };
}
