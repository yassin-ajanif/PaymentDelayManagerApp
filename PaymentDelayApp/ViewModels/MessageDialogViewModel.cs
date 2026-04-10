using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PaymentDelayApp.ViewModels;

public partial class MessageDialogViewModel : ViewModelBase
{
    private readonly Window _window;

    public MessageDialogViewModel(Window window, string title, string message)
    {
        _window = window;
        Title = title;
        Message = message;
    }

    public string Title { get; }
    public string Message { get; }

    [RelayCommand]
    private void Ok() => _window.Close(true);
}
