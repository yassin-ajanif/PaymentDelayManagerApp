using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PaymentDelayApp.ViewModels;

public partial class ReglementDialogViewModel : ViewModelBase
{
    private readonly Window _window;

    public ReglementDialogViewModel(Window window)
    {
        _window = window;
        var n = DateTime.Now;
        SelectedDate = new DateTimeOffset(DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Local));
        Hour = n.Hour;
        Minute = n.Minute;
    }

    [ObservableProperty]
    private DateTimeOffset? _selectedDate;

    [ObservableProperty]
    private int _hour;

    [ObservableProperty]
    private int _minute;

    [RelayCommand]
    private void Confirm()
    {
        var date = SelectedDate?.LocalDateTime.Date ?? DateTime.Today;
        var dt = new DateTime(date.Year, date.Month, date.Day, Hour, Minute, 0, DateTimeKind.Local);
        _window.Close(dt);
    }

    [RelayCommand]
    private void Cancel() => _window.Close(null);
}
