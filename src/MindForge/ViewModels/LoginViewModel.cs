using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Services.Interfaces;
using System.Threading.Tasks;

namespace MindForge.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    
    [ObservableProperty]
    private string _email = string.Empty;
    
    [ObservableProperty]
    private string _password = string.Empty;
    
    [ObservableProperty]
    private string _errorMessage = string.Empty;
    
    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }
    
    [RelayCommand]
    private async Task LoginAsync()
    {
    }
    
    [RelayCommand]
    private async Task RegisterAsync()
    {
    }
}
