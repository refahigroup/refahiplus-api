using System;
using System.Threading;
using System.Threading.Tasks;
using Refahi.Modules.Identity.Domain.Aggregates;

namespace Refahi.Modules.Identity.Application.Contracts.Interfaces;

public interface IAuthService
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByMobileOrEmailAsync(string mobileOrEmail, CancellationToken cancellationToken = default);
}
