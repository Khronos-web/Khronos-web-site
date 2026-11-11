using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Codec;

public partial class App : Application
{
    private Process? _aiServerProcess;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        StartAiServer();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            string configPath = "user_config.txt";
            bool isConfigValid = false;
            string username = string.Empty;

            if (File.Exists(configPath))
            {
                string[] lines = File.ReadAllLines(configPath);
                if (lines.Length > 0 && !string.IsNullOrWhiteSpace(lines[0]))
                {
                    username = lines[0].Trim();
                    isConfigValid = true;
                }
            }

            if (isConfigValid)
            {
                OpenMainWindow(desktop, username);
            }
            else
            {
                ShowLoginWindow(desktop);
            }

            desktop.Exit += (sender, args) => StopAiServer();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowLoginWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var loginWindow = new LoginWindow();
        var loginViewModel = new LoginViewModel();

        loginViewModel.OnLoginSuccess += (userName) =>
        {
            OpenMainWindow(desktop, userName);
            loginWindow.Close();
        };

        loginWindow.DataContext = loginViewModel;
        desktop.MainWindow = loginWindow;
        loginWindow.Show();
    }

    private void OpenMainWindow(IClassicDesktopStyleApplicationLifetime desktop, string username)
    {
        var viewModel = new MainViewModel();
        if (viewModel.Messages.Count == 0)
        {
            viewModel.Messages.Add(string.Format("Codec → Привет, {0}! Чем могу помочь?", username));
        }

        var mainWindow = new MainWindow
        {
            DataContext = viewModel
        };
        desktop.MainWindow = mainWindow;
        mainWindow.Show();
    }

    private void StartAiServer()
    {
        try
        {
            string scriptDir = AppDomain.CurrentDomain.BaseDirectory;
            string scriptPath = Path.Combine(scriptDir, "server.py");

            if (!File.Exists(scriptPath)) return;

            var startInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "python" : "python3",
                Arguments = string.Format("\"{0}\"", scriptPath),
                WorkingDirectory = scriptDir,
                UseShellExecute = true,
                CreateNoWindow = false
            };

            _aiServerProcess = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    private void StopAiServer()
    {
        try
        {
            if (_aiServerProcess != null && !_aiServerProcess.HasExited)
            {
                _aiServerProcess.Kill(true);
                _aiServerProcess.Dispose();
            }
        }
        catch { }
    }
}