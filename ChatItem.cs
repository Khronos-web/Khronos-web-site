using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Codec;

public partial class ChatItem : ObservableObject
{
    [ObservableProperty]
    private string _title;

    // Текст в поле ввода во время переименования
    [ObservableProperty]
    private string _editTitle = string.Empty;

    [ObservableProperty]
    private bool _isEditing;

    public ObservableCollection<string> Messages { get; } = new();

    public ChatItem(string title)
    {
        _title = title;
    }
}
