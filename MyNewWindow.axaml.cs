using Avalonia.Controls;
using Avalonia.Interactivity;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Codec;

public partial class MyNewWindow : Window, INotifyPropertyChanged
{
    private static readonly Avalonia.Media.IBrush GreenBorder =
        new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1b6d13"));

    private MainWindow? _mainWindow;
    private MainViewModel? _viewModel;

    public string ChangeButtonText => _viewModel != null && _viewModel.IsRussian ? "English" : "Русский";

    public MyNewWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public MyNewWindow(MainWindow mainWindow, MainViewModel viewModel) : this()
    {
        _mainWindow = mainWindow;
        _viewModel = viewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.IsDarkMode))
                {
                    UpdateTheme();
                }
            };
            UpdateTheme();
        }

        this.FindControl<Button>("CloseBtn")?.Click += (s, e) => Close();
    }

    private void UpdateTheme()
    {
        if (_viewModel != null)
        {
            var bg = _viewModel.IsDarkMode ? Avalonia.Media.Brushes.Black : Avalonia.Media.Brushes.White;
            var fg = _viewModel.IsDarkMode ? Avalonia.Media.Brushes.White : Avalonia.Media.Brushes.Black;

            Background = bg;
            
            var toggleBtn = this.FindControl<Button>("ToggleBtn");
            var langBtn = this.FindControl<Button>("LangBtn");
            var closeBtn = this.FindControl<Button>("CloseBtn");
            var versionText = this.FindControl<TextBlock>("VersionText");

            if (toggleBtn != null) { toggleBtn.Background = bg; toggleBtn.Foreground = fg; }
if (langBtn != null) { langBtn.Background = bg; langBtn.Foreground = fg; }
if (closeBtn != null) { closeBtn.Background = bg; closeBtn.Foreground = fg; }
            if (versionText != null) { versionText.Foreground = fg; }
        }
    }

    private void ToggleBtn_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel?.ToggleThemeCommand.Execute(null);
    }

    private void OnToggleLanguageClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.ToggleLanguageCommand.Execute(null);
            OnPropertyChanged(nameof(ChangeButtonText));
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}