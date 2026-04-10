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

    /// <summary>Fallback widths by column index (index 4 = Désignation, unused).</summary>
    private static readonly double[] NominalColumnWidths =
    [
        120, 160, 110, 200, 0, 90, 130, 150, 110, 140,
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

        if (Math.Abs(available - _lastDesignationWidth) < 0.5)
            return;
        _lastDesignationWidth = available;

        grid.Columns[DesignationColumnIndex].Width = new DataGridLength(available);
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

        var edit = new MenuItem
        {
            Header = RowContextHeader("✎", "Modifier"),
            Command = vm.EditInvoiceCommand,
            CommandParameter = invoiceRow,
        };
        var delete = new MenuItem
        {
            Header = RowContextHeader("🗑", "Supprimer"),
            Command = vm.DeleteInvoiceCommand,
            CommandParameter = invoiceRow,
        };
        var regler = new MenuItem
        {
            Header = RowContextHeader("💰", "Régler"),
            Command = vm.ReglerInvoiceCommand,
            CommandParameter = invoiceRow,
            IsEnabled = invoiceRow.CanRegler,
        };

        var menu = new ContextMenu();
        menu.Items.Add(edit);
        menu.Items.Add(delete);
        menu.Items.Add(regler);
        menu.Open(row);
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
