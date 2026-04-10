using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PaymentDelayApp.ViewModels;

public partial class ConfirmDialogViewModel : ViewModelBase
{
    private readonly Window _window;

    public ConfirmDialogViewModel(Window window, string title, string message)
    {
        _window = window;
        Title = title;
        Message = message;
    }

    public string Title { get; }
    public string Message { get; }

    [RelayCommand]
    private void Yes() => _window.Close(true);

    [RelayCommand]
    private void No() => _window.Close(false);
}
