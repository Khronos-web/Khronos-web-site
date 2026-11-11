using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Media;

namespace Codec;

public partial class MainViewModel : ObservableObject
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    private const string ServerUrl = "http://127.0.0.1:8000/chat";
    private const string HealthUrl = "http://127.0.0.1:8000/";

    private const string ServerReadyMessage = "Codec → Codec успешно запущен и готов принимать сообщения!";
    private const string ServerWarnMessage = "Codec → Предупреждение: не удалось достугнуться до сервера.";

    private static readonly string ChatsPath = Path.Combine(AppContext.BaseDirectory, "chats.txt");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private int _chatCounter = 0;
    private int _savedSelectedIndex = 0;

    public ObservableCollection<ChatItem> Chats { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Messages))]
    private ChatItem _selectedChat;

    public ObservableCollection<string> Messages => SelectedChat.Messages;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    [NotifyPropertyChangedFor(nameof(PlaceholderText))]
    private bool _isServerReady;

    [ObservableProperty]
    private string _inputMessage = string.Empty;

    [ObservableProperty]
    private bool _isDarkMode = true;

    [ObservableProperty]
    private bool _isRussian = true;

    public IBrush BackgroundColor => IsDarkMode ? Brushes.Black : Brushes.White;
    public IBrush ForegroundColor => IsDarkMode ? Brushes.White : Brushes.Black;

    public string SettingsButtonText => IsRussian ? "Настройки" : "Settings";
    public string PromptHintText => IsRussian ? "Пиши что думаешь?" : "What's on your mind?";
    public string PlaceholderText => !IsServerReady
        ? (IsRussian ? "Сервер запускается, подождите..." : "Server is starting, please wait...")
        : (IsRussian ? "Введите сообщение..." : "Type a message...");
    public string SendButtonText => IsRussian ? "Отправить" : "Send";
    public string NewChatButtonText => IsRussian ? "+ Новый чат" : "+ New chat";
    public string RenameTooltip => IsRussian ? "Переименовать" : "Rename";
    public string Aifalse => IsRussian ? "Ии может ошибатся проверяйте информацию!" : "AI can make mistakes. Check important info!";
    public string CurrentDate => DateTime.Now.ToString("dd.MM.yyyy HH:mm");

    public MainViewModel()
    {
        LoadChats();

        if (Chats.Count == 0)
        {
            Chats.Add(CreateChat());
        }

        int index = Math.Clamp(_savedSelectedIndex, 0, Chats.Count - 1);
        _selectedChat = Chats[index];

        _ = CheckServerAndNotifyAsync();
    }

    partial void OnSelectedChatChanged(ChatItem value)
    {
        SaveChats();
    }

    private ChatItem CreateChat()
    {
        _chatCounter++;
        var chat = new ChatItem($"{(IsRussian ? "Чат" : "Chat")} {_chatCounter}");
        Attach(chat);
        return chat;
    }

    private void Attach(ChatItem chat)
    {
        chat.Messages.CollectionChanged += (_, _) => SaveChats();
        chat.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ChatItem.Title))
                SaveChats();
        };
    }

    private static bool IsSystemMessage(string message)
    {
        return message == ServerReadyMessage || message == ServerWarnMessage;
    }

    private void LoadChats()
    {
        try
        {
            if (!File.Exists(ChatsPath)) return;

            string json = File.ReadAllText(ChatsPath, Encoding.UTF8);
            var data = JsonSerializer.Deserialize<ChatsData>(json);
            if (data == null) return;

            foreach (var saved in data.Chats)
            {
                string title = string.IsNullOrWhiteSpace(saved.Title) ? "Чат" : saved.Title;
                var chat = new ChatItem(title);

                foreach (var message in saved.Messages)
                    chat.Messages.Add(message);

                Attach(chat);
                Chats.Add(chat);
            }

            _chatCounter = Math.Max(data.Counter, Chats.Count);
            _savedSelectedIndex = data.Selected;
        }
        catch
        {
        }
    }

    private void SaveChats()
    {
        try
        {
            var data = new ChatsData
            {
                Counter = _chatCounter,
                Selected = Math.Max(0, Chats.IndexOf(SelectedChat)),
                Chats = Chats.Select(c => new SavedChat
                {
                    Title = c.Title,
                    Messages = c.Messages.Where(m => !IsSystemMessage(m)).ToList()
                }).ToList()
            };

            string json = JsonSerializer.Serialize(data, JsonOptions);
            string tempPath = ChatsPath + ".tmp";

            File.WriteAllText(tempPath, json, Encoding.UTF8);
            File.Move(tempPath, ChatsPath, true);
        }
        catch
        {
        }
    }

    private async Task CheckServerAndNotifyAsync()
    {
        var chat = SelectedChat;

        for (int i = 0; i < 60; i++)
        {
            try
            {
                var response = await _httpClient.GetAsync(HealthUrl);
                if (response.IsSuccessStatusCode)
                {
                    IsServerReady = true;
                    chat.Messages.Add(ServerReadyMessage);
                    return;
                }
            }
            catch
            {
            }

            await Task.Delay(1000);
        }

        IsServerReady = true;
        chat.Messages.Add(ServerWarnMessage);
    }

    private bool CanSendMessage() => IsServerReady;

    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    public async Task SendMessageAsync()
    {
        if (!IsServerReady || string.IsNullOrWhiteSpace(InputMessage)) return;

        var chat = SelectedChat;

        string userMsg = InputMessage.Trim();
        chat.Messages.Add($"Вы → {userMsg}");
        InputMessage = string.Empty;

        try
        {
            var payload = new { message = userMsg };
            string json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(ServerUrl, content);
            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();

                try
                {
                    using var doc = JsonDocument.Parse(responseString);
                    string answer = doc.RootElement.GetProperty("response").GetString() ?? responseString;
                    chat.Messages.Add($"Codec → {answer}");
                }
                catch
                {
                    chat.Messages.Add($"Codec → {responseString}");
                }
            }
            else
            {
                chat.Messages.Add($"Codec → Ошибка: сервер вернул статус {response.StatusCode}.");
            }
        }
        catch (Exception)
        {
            chat.Messages.Add("Codec → Ошибка: не удалось подключиться к локальному серверу Python.");
        }
    }

    [RelayCommand]
    public void NewChat()
    {
        var chat = CreateChat();
        Chats.Add(chat);
        SelectedChat = chat;
    }

    [RelayCommand]
    public void StartRename(ChatItem? chat)
    {
        chat ??= SelectedChat;

        foreach (var c in Chats)
            c.IsEditing = false;

        chat.EditTitle = chat.Title;
        chat.IsEditing = true;
    }

    [RelayCommand]
    public void CommitRename(ChatItem? chat)
    {
        if (chat == null || !chat.IsEditing) return;

        string newTitle = chat.EditTitle.Trim();
        if (newTitle.Length > 0)
            chat.Title = newTitle;

        chat.IsEditing = false;
    }

    [RelayCommand]
    public void CancelRename(ChatItem? chat)
    {
        if (chat == null) return;
        chat.IsEditing = false;
    }

    [RelayCommand]
    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(ForegroundColor));
    }

    [RelayCommand]
    public void ToggleLanguage()
    {
        IsRussian = !IsRussian;
        OnPropertyChanged(nameof(SettingsButtonText));
        OnPropertyChanged(nameof(PromptHintText));
        OnPropertyChanged(nameof(PlaceholderText));
        OnPropertyChanged(nameof(SendButtonText));
        OnPropertyChanged(nameof(NewChatButtonText));
        OnPropertyChanged(nameof(RenameTooltip));
        OnPropertyChanged(nameof(Aifalse));
    }

    [RelayCommand]
    public void OpenSettings()
    {
        var desktop = Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        var mainWindow = desktop?.MainWindow as MainWindow;

        if (mainWindow != null)
        {
            var settingsWindow = new MyNewWindow(mainWindow, this);
            settingsWindow.Show();
        }
    }
}