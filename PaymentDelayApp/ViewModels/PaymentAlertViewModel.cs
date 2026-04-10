using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaymentDelayApp.Models;

namespace PaymentDelayApp.ViewModels;

public partial class PaymentAlertViewModel : ViewModelBase
{
    private readonly Window _window;

    public PaymentAlertViewModel(Window window, IReadOnlyList<PaymentAlertLine> lines)
    {
        _window = window;
        Lines = new ObservableCollection<PaymentAlertLine>(lines);
    }

    public ObservableCollection<PaymentAlertLine> Lines { get; }

    [RelayCommand]
    private void Ok() => _window.Close(true);
}
