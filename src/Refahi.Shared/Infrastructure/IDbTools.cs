namespace Refahi.Shared.Infrastructure;

public interface IDbTools
{
    void ApplyMigrations<T>();
}
