using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Domain.Aggregates;
using Refahi.Modules.Identity.Domain.Repositories;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreateWallet;
using Refahi.Shared.Services.Notification;
using DomainRoles = Refahi.Modules.Identity.Domain.ValueObjects.Roles;

namespace Refahi.Modules.Identity.Application.Features.Auth.Registration;

public sealed class UserRegistrationService : IUserRegistrationService
{
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<UserRegistrationService> _logger;
    private readonly IMediator _mediator;

    public UserRegistrationService(
        IUserRepository userRepository,
        INotificationService notificationService,
        ILogger<UserRegistrationService> logger,
        IMediator mediator)
    {
        _userRepository = userRepository;
        _notificationService = notificationService;
        _logger = logger;
        _mediator = mediator;
    }

    public async Task<RegistrationResult> RegisterAsync(
        string? mobileNumber,
        string? email,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mobileNumber) && string.IsNullOrWhiteSpace(email))
            return new RegistrationResult(false, "Either mobile number or email is required");

        if (!string.IsNullOrWhiteSpace(mobileNumber) &&
            await _userRepository.ExistsByMobileNumberAsync(mobileNumber, cancellationToken))
        {
            return new RegistrationResult(false, "User with this mobile number already exists");
        }

        if (!string.IsNullOrWhiteSpace(email) &&
            await _userRepository.ExistsByEmailAsync(email, cancellationToken))
        {
            return new RegistrationResult(false, "User with this email already exists");
        }

        var user = User.Create(mobileNumber, email);
        user.AssignRole(DomainRoles.User);

        await _userRepository.AddAsync(user, cancellationToken);

        try
        {
            await _mediator.Send(new CreateWalletCommand(user.Id, "REFAHI", "IRR"), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-provision wallet for user {UserId}", user.Id);
        }

        if (!string.IsNullOrWhiteSpace(mobileNumber))
        {
            try
            {
                await _notificationService.SendSms(
                    phoneNumbers: new[] { mobileNumber },
                    body: "به رفاهی پلاس خوش آمدید! ثبت‌نام شما با موفقیت انجام شد. زمان: {{time}}",
                    sender: "10008580",
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Welcome SMS sent to user {UserId}", user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send welcome SMS to user {UserId}", user.Id);
            }
        }

        var userDto = new UserDto(
            user.Id,
            user.MobileNumber,
            user.Email,
            user.IsActive,
            user.GetRoles());

        return new RegistrationResult(true, null, userDto, mobileNumber, email);
    }
}
