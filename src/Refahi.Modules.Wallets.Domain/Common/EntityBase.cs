using Refahi.Modules.Wallets.Domain.Events;
using Refahi.Shared.Domain;
using System.Collections.Generic;
using System.Linq;

namespace Refahi.Modules.Wallets.Domain.Common;

/// <summary>
/// Base class for entities with domain event support.
/// </summary>
public abstract class EntityBase
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public bool HasDomainEvents() => _domainEvents.Any();
}
