using System.Threading.Tasks;
using Identity.Application.Contracts.Models;
using Identity.Domain.Aggregates;

namespace Identity.Application.Contracts.Interfaces;

public interface IUserRepository
{
    Task<UserAggregate> GetByUsername(string username);
    Task<UserAggregate> GetByIdAsync(string id);
}
