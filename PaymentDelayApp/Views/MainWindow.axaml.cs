using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PaymentDelayApp.Models;
using PaymentDelayApp.ViewModels;

namespace PaymentDelayApp.Views;

public partial class MainWindow : Window
{
    private const int DesignationColumnIndex = 4;

    /// <summary>Fallback widths when <see cref="DataGridColumn.ActualWidth"/> is not ready yet (index 4 = Désignation, unused).</summary>
    private static readonly double[] NominalColumnWidths =
    [
        100, 100, 110, 200, 0, 90, 142, 72, 100,
    ];

    private double _lastDesignationWidth;
    private bool _adjustPosted;

    public MainWindow()
    {
        InitializeComponent();
        // Let header row grow with wrapped text (Fluent default is a single-line height).
        InvoiceGrid.ColumnHeaderHeight = double.NaN;
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        SizeChanged += (_, _) => ScheduleAdjustDesignationColumn();
        InvoiceGrid.SizeChanged += (_, _) => ScheduleAdjustDesignationColumn();
        InvoiceGrid.LayoutUpdated += (_, _) => ScheduleAdjustDesignationColumn();
        ScheduleAdjustDesignationColumn();
    }

    private void ScheduleAdjustDesignationColumn()
    {
        if (_adjustPosted)
            return;
        _adjustPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _adjustPosted = false;
            AdjustDesignationColumnWidth();
        }, DispatcherPriority.Background);
    }

    private void AdjustDesignationColumnWidth()
    {
        var grid = InvoiceGrid;
        if (grid.Columns.Count <= DesignationColumnIndex)
            return;

        double otherTotal = 0;
        for (var i = 0; i < grid.Columns.Count; i++)
        {
            if (i == DesignationColumnIndex)
                continue;
            var col = grid.Columns[i];
            var aw = col.ActualWidth;
            otherTotal += aw > 0 ? aw : NominalColumnWidths[i];
        }

        // Row headers + vertical scrollbar chrome
        const double chrome = 28;
        var available = grid.Bounds.Width - otherTotal - chrome;
        const double minW = 140;
        if (available < minW)
            available = minW;

        // If Désignation == exact remainder, total width matches the viewport and the horizontal scrollbar
        // cannot move (last columns stay clipped). Widen slightly when the last column is under-sized.
        const double lastColumnMinWidth = 112;
        const double resteJoursMinWidth = 40;
        var slack = 0.0;
        if (grid.Columns.Count > DesignationColumnIndex + 1)
        {
            var lastCol = grid.Columns[^1];
            var lastAw = lastCol.ActualWidth;
            if (lastAw < lastColumnMinWidth - 0.5)
                slack = lastColumnMinWidth - lastAw + 24;
        }

        if (grid.Columns.Count > 7)
        {
            var resteAw = grid.Columns[7].ActualWidth;
            if (resteAw < resteJoursMinWidth - 0.5)
                slack = Math.Max(slack, resteJoursMinWidth - resteAw + 16);
        }

        var designationWidth = Math.Max(minW, available + slack);

        if (Math.Abs(designationWidth - _lastDesignationWidth) < 0.5)
            return;
        _lastDesignationWidth = designationWidth;

        grid.Columns[DesignationColumnIndex].Width = new DataGridLength(designationWidth);
    }

    private void InvoiceGrid_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Right)
            return;
        if (sender is not DataGrid grid || grid.DataContext is not DashboardViewModel vm)
            return;
        if (e.Source is not Visual source)
            return;

        var row = source.FindAncestorOfType<DataGridRow>();
        if (row?.DataContext is not InvoiceDashboardRow invoiceRow)
            return;

        vm.SelectedRow = invoiceRow;

        var delete = new MenuItem
        {
            Header = RowContextHeader("🗑", "Supprimer"),
            Command = vm.DeleteInvoiceCommand,
            CommandParameter = invoiceRow,
            IsEnabled = !invoiceRow.IsSettled,
        };
        var regler = new MenuItem
        {
            Header = RowContextHeader("💰", "Régler"),
            Command = vm.ReglerInvoiceCommand,
            CommandParameter = invoiceRow,
            IsEnabled = invoiceRow.CanRegler,
        };
        var unsettle = new MenuItem
        {
            Header = RowContextHeader("↩", "Annuler le règlement"),
            Command = vm.UnsettleInvoiceCommand,
            CommandParameter = invoiceRow,
            IsEnabled = invoiceRow.CanUnsettle,
        };

        var menu = new ContextMenu();
        menu.Items.Add(delete);
        menu.Items.Add(regler);
        menu.Items.Add(unsettle);
        menu.Open(row);
    }

    private void InvoiceGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not DataGrid grid || grid.DataContext is not DashboardViewModel vm)
            return;
        if (e.Source is not Visual source)
            return;
        var row = source.FindAncestorOfType<DataGridRow>();
        if (row?.DataContext is not InvoiceDashboardRow invoiceRow)
            return;
        vm.SelectedRow = invoiceRow;
        vm.EditInvoiceCommand.Execute(invoiceRow);
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
