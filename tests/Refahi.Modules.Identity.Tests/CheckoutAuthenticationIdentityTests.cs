using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Identity.Application.Features.Auth;
using Refahi.Modules.Identity.Domain.Aggregates;
using Refahi.Modules.Identity.Domain.Repositories;
using Refahi.Shared.Services.Notification;
using Xunit;

namespace Refahi.Modules.Identity.Tests;

public sealed class CheckoutAuthenticationIdentityTests
{
    [Fact]
    public async Task Mobile_and_username_resolve_only_to_identity_owned_approved_receipts()
    {
        var mobileUser = User.Create("09121234567", null);
        mobileUser.ApproveMobile();
        mobileUser.SetUsername("alice");
        var emailUser = User.Create(null, "trusted@example.com");
        emailUser.ApproveEmail();
        emailUser.SetUsername("bob");
        var handler = new ResolveAuthenticationIdentityQueryHandler(new FakeUsers([mobileUser, emailUser]));

        var byMobile = await handler.Handle(new ResolveAuthenticationIdentityQuery("09121234567"), default);
        var byUsername = await handler.Handle(new ResolveAuthenticationIdentityQuery("ALICE"), default);
        var emailFallback = await handler.Handle(new ResolveAuthenticationIdentityQuery("bob"), default);

        Assert.Equal(mobileUser.Id, byMobile!.UserId);
        Assert.Equal("09121234567", byUsername!.OtpReceipt);
        Assert.Equal(OtpReceiptType.Sms, byUsername.OtpReceiptType);
        Assert.Equal("trusted@example.com", emailFallback!.OtpReceipt);
        Assert.Equal(OtpReceiptType.Email, emailFallback.OtpReceiptType);
        Assert.Null(await handler.Handle(new ResolveAuthenticationIdentityQuery("trusted@example.com"), default));
    }

    [Fact]
    public async Task Inactive_locked_unapproved_and_unknown_identities_cannot_authenticate()
    {
        var inactive = User.Create("09120000001", null); inactive.ApproveMobile(); inactive.SetUsername("inactive"); inactive.Deactivate();
        var locked = User.Create("09120000002", null); locked.ApproveMobile(); locked.SetUsername("locked"); locked.Lock(TimeSpan.FromMinutes(5));
        var unapproved = User.Create("09120000003", null); unapproved.SetUsername("unapproved");
        var users = new FakeUsers([inactive, locked, unapproved]);
        var resolve = new ResolveAuthenticationIdentityQueryHandler(users);
        var eligibility = new GetUserAuthenticationEligibilityQueryHandler(users);

        Assert.False((await resolve.Handle(new("inactive"), default))!.CanAuthenticate);
        Assert.False((await resolve.Handle(new("locked"), default))!.CanAuthenticate);
        Assert.False((await resolve.Handle(new("unapproved"), default))!.CanAuthenticate);
        Assert.Null(await resolve.Handle(new("unknown"), default));
        Assert.False((await eligibility.Handle(new(inactive.Id), default)).CanAuthenticate);
        Assert.False((await eligibility.Handle(new(Guid.NewGuid()), default)).CanAuthenticate);
    }

    private sealed class FakeUsers(IReadOnlyCollection<User> users) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(users.SingleOrDefault(x=>x.Id==id));
        public Task<User?> GetByMobileNumberAsync(string value,CancellationToken ct=default)=>Task.FromResult(users.SingleOrDefault(x=>x.MobileNumber==value));
        public Task<User?> GetByEmailAsync(string value,CancellationToken ct=default)=>Task.FromResult(users.SingleOrDefault(x=>x.Email==value.ToLowerInvariant()));
        public Task<User?> GetByUsernameAsync(string value,CancellationToken ct=default)=>Task.FromResult(users.SingleOrDefault(x=>x.Username==value.ToLowerInvariant()));
        public Task<User?> GetByMobileOrEmailAsync(string value,CancellationToken ct=default)=>Task.FromResult(users.SingleOrDefault(x=>x.MobileNumber==value||x.Email==value.ToLowerInvariant()));
        public Task<User?> GetByMobileOrEmailOrUsernameAsync(string value,CancellationToken ct=default){var normalized=value.Trim().ToLowerInvariant();return Task.FromResult(users.SingleOrDefault(x=>x.MobileNumber==value.Trim()||x.Email==normalized||x.Username==normalized));}
        public Task<bool> ExistsByMobileNumberAsync(string value,CancellationToken ct=default)=>Task.FromResult(users.Any(x=>x.MobileNumber==value));
        public Task<bool> ExistsByEmailAsync(string value,CancellationToken ct=default)=>Task.FromResult(users.Any(x=>x.Email==value.ToLowerInvariant()));
        public Task<bool> ExistsByUsernameAsync(string value,CancellationToken ct=default)=>Task.FromResult(users.Any(x=>x.Username==value.ToLowerInvariant()));
        public Task<(List<User> Items,int Total)> GetPagedAsync(string? s,string? r,bool? a,int p,int ps,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<List<User>> GetByIdsAsync(IReadOnlyCollection<Guid> ids,CancellationToken ct=default)=>Task.FromResult(users.Where(x=>ids.Contains(x.Id)).ToList());
        public Task<List<User>> SearchByMobileNumberAsync(string value,CancellationToken ct=default)=>Task.FromResult(users.Where(x=>x.MobileNumber?.Contains(value)==true).ToList());
        public Task AddAsync(User user,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task UpdateAsync(User user,CancellationToken ct=default)=>throw new NotSupportedException();
    }
}
