using System.Threading.Tasks;
using Identity.Application.Contracts.Models;

namespace Identity.Application.Contracts.Interfaces;

public interface IAuthService
{
    Task<UserDto?> ValidateCredentialsAsync(string username, string password);
    Task<UserDto?> GetByIdAsync(string id);
}
