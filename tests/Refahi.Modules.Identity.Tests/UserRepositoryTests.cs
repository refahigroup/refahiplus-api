using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Refahi.Modules.Identity.Domain.Aggregates;
using Refahi.Modules.Identity.Domain.Entities;
using Refahi.Modules.Identity.Infrastructure.Persistence.Context;
using Refahi.Modules.Identity.Infrastructure.Repositories;
using Xunit;

namespace Refahi.Modules.Identity.Tests;

public sealed class UserRepositoryTests
{
    [Fact]
    public async Task UpdateAsync_DoesNotMarkTrackedNavigationEntitiesAsModified()
    {
        await using var db = CreateDbContext();
        var user = User.Create("09120000000", null);
        user.AssignRole("User");

        db.Users.Attach(user);
        user.Deactivate();

        var repository = new UserRepository(db);

        await repository.UpdateAsync(user);

        Assert.Equal(EntityState.Modified, db.Entry(user).State);
        Assert.All(
            db.ChangeTracker.Entries<UserRole>(),
            entry => Assert.Equal(EntityState.Unchanged, entry.State)
        );
    }

    [Fact]
    public async Task UpdateAsync_TracksRoleAddedToTrackedUserAsAdded()
    {
        UserRole? newRole = null;
        await using var db = CreateDbContext(context =>
        {
            Assert.NotNull(newRole);
            Assert.Equal(EntityState.Added, context.Entry(newRole).State);
        });
        var user = User.Create("09120000001", null);

        db.Users.Attach(user);
        user.AssignRole("Admin");
        newRole = Assert.Single(user.Roles);
        Assert.Equal(EntityState.Detached, db.Entry(newRole).State);

        var repository = new UserRepository(db);

        await repository.UpdateAsync(user);
    }

    private static IdentityDbContext CreateDbContext(Action<DbContext>? onSaving = null)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=unused;Database=unused;Username=unused;Password=unused")
            .AddInterceptors(new SuppressSaveChangesInterceptor(onSaving))
            .Options;

        return new IdentityDbContext(options);
    }

    private sealed class SuppressSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly Action<DbContext>? _onSaving;

        public SuppressSaveChangesInterceptor(Action<DbContext>? onSaving)
        {
            _onSaving = onSaving;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            if (eventData.Context is not null)
                _onSaving?.Invoke(eventData.Context);

            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
        }
    }
}
