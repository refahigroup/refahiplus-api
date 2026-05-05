using Refahi.Modules.Identity.Domain.Entities;

namespace Refahi.Modules.Identity.Domain.Repositories;

/// <summary>
/// Repository برای آدرس‌های کاربر.
/// </summary>
public interface IUserAddressRepository
{
    /// <summary>دریافت یک آدرس بر اساس Id.</summary>
    Task<UserAddress?> GetByIdAsync(Guid addressId, CancellationToken cancellationToken = default);

    /// <summary>دریافت یک آدرس متعلق به کاربر مشخص.</summary>
    Task<UserAddress?> GetByIdForUserAsync(Guid addressId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>لیست آدرس‌های یک کاربر — مرتب‌شده بر اساس IsDefault (پیش‌فرض اول)، سپس CreatedAt نزولی.</summary>
    Task<IReadOnlyList<UserAddress>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>دریافت آدرس پیش‌فرض کاربر (در صورت وجود).</summary>
    Task<UserAddress?> GetDefaultForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(UserAddress address, CancellationToken cancellationToken = default);

    Task UpdateAsync(UserAddress address, CancellationToken cancellationToken = default);

    Task DeleteAsync(UserAddress address, CancellationToken cancellationToken = default);

    /// <summary>
    /// همه‌ی آدرس‌های دیگر کاربر را از حالت پیش‌فرض خارج می‌کند.
    /// (به‌غیر از addressId اگر مقدار داشته باشد.)
    /// </summary>
    Task UnsetDefaultForUserAsync(Guid userId, Guid? exceptAddressId = null, CancellationToken cancellationToken = default);
}
