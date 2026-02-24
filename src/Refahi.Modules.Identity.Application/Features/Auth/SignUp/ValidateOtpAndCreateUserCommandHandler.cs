using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Domain.Aggregates;
using Refahi.Modules.Identity.Domain.Repositories;
using Refahi.Shared.Services.Notification;
using DomainRoles = Refahi.Modules.Identity.Domain.ValueObjects.Roles;

namespace Refahi.Modules.Identity.Application.Features.Auth.SignUp;

public class ValidateOtpAndCreateUserCommandHandler : IRequestHandler<ValidateOtpAndCreateUserCommand, ValidateOtpResult>
{
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ValidateOtpAndCreateUserCommandHandler> _logger;

    public ValidateOtpAndCreateUserCommandHandler(
        IUserRepository userRepository,
        INotificationService notificationService,
        ILogger<ValidateOtpAndCreateUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<ValidateOtpResult> Handle(ValidateOtpAndCreateUserCommand request, CancellationToken cancellationToken)
    {
        // Note: Basic validation (OTP format) is handled by FluentValidation pipeline
        
        // Validate OTP via notification service
        var validationResult = await _notificationService.ValidateOtp(request.Token, request.OtpCode, OtpType.SignUp, cancellationToken);

        if (!validationResult.IsValid)
        {
            return new ValidateOtpResult(false, "Invalid or expired OTP code");
        }

        // Extract mobile/email from validation result
        string? mobileNumber = null;
        string? email = null;

        if (validationResult.ReceiptType == OtpReceiptType.Sms)
        {
            mobileNumber = validationResult.Receipt;
        }
        else if (validationResult.ReceiptType == OtpReceiptType.Email)
        {
            email = validationResult.Receipt;
        }

        // Double-check if user already exists (safety check, shouldn't happen if SendOtp validated correctly)
        if (!string.IsNullOrWhiteSpace(mobileNumber) && await _userRepository.ExistsByMobileNumberAsync(mobileNumber, cancellationToken))
        {
            return new ValidateOtpResult(false, "User with this mobile number already exists");
        }

        if (!string.IsNullOrWhiteSpace(email) && await _userRepository.ExistsByEmailAsync(email, cancellationToken))
        {
            return new ValidateOtpResult(false, "User with this email already exists");
        }

        // Create new user
        var user = User.Create(
            mobileNumber: mobileNumber,
            email: email);

        // Assign default User role
        user.AssignRole(DomainRoles.User);

        // Save user
        await _userRepository.AddAsync(user, cancellationToken);

        // Send welcome SMS notification if mobile number is available
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
                // Log error but don't fail the registration
                _logger.LogError(ex, "Failed to send welcome SMS to user {UserId}", user.Id);
            }
        }

        // Return user DTO
        var userDto = new UserDto(
            user.Id,
            user.MobileNumber,
            user.Email,
            user.IsActive,
            user.GetRoles());

        return new ValidateOtpResult(true, null, userDto, mobileNumber, email);
    }
}
