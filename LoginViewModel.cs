using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Codec;

public partial class LoginViewModel : ObservableObject
{
    [ObservableProperty]
    private string _usernameInput = string.Empty;

    [ObservableProperty]
    private string _passwordInput = string.Empty;

    public event Action<string>? OnLoginSuccess;

    [RelayCommand]
    private void Login()
    {
        if (!string.IsNullOrWhiteSpace(UsernameInput))
        {
            string configContent = string.Format("{0}\n{1}", UsernameInput, PasswordInput);
            File.WriteAllText("user_config.txt", configContent);

            OnLoginSuccess?.Invoke(UsernameInput);
        }
    }
}