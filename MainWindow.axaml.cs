using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Codec;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;


    private void ChatItem_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: ChatItem chat })
        {
            Vm?.StartRenameCommand.Execute(chat);
            e.Handled = true;
        }
    }


    private void RenameButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ChatItem chat })
        {
            Vm?.StartRenameCommand.Execute(chat);
        }
    }

   
    private void RenameBox_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;

        tb.PropertyChanged += (_, args) =>
        {
            if (args.Property == IsVisibleProperty && args.NewValue is true)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    tb.Focus();
                    tb.SelectAll();
                }, DispatcherPriority.Input);
            }
        };
    }

    
    private void RenameBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: ChatItem chat }) return;

        if (e.Key == Key.Enter)
        {
            Vm?.CommitRenameCommand.Execute(chat);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Vm?.CancelRenameCommand.Execute(chat);
            e.Handled = true;
        }
    }

   
    private void RenameBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: ChatItem chat })
        {
            Vm?.CommitRenameCommand.Execute(chat);
        }
    }
}