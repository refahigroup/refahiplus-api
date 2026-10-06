namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ITicketQueryNameAttribute : Attribute
{
    public ITicketQueryNameAttribute(string name) => Name = name;
    public string Name { get; }
}
