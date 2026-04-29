using MindForge.Models;
using System.Threading.Tasks;

namespace MindForge.Services.Interfaces;

public interface IAuthService
{
    Task<Result<User>> RegisterAsync(string username, string email, string password);
    Task<Result<User>> LoginAsync(string email, string password);
    void Logout();
}

public class Result<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    
    public static Result<T> Success(T data) => new() { IsSuccess = true, Data = data };
    public static Result<T> Failure(string error) => new() { IsSuccess = false, ErrorMessage = error };
}
