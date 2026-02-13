using System.Collections.Generic;
using System.Linq;

namespace Wallets.Domain.Common;

/// <summary>
/// Base class for entities with domain event support.
/// </summary>
public abstract class EntityBase
{
    private readonly List<Events.IDomainEvent> _domainEvents = new();

    public IReadOnlyList<Events.IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(Events.IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public bool HasDomainEvents() => _domainEvents.Any();
}
