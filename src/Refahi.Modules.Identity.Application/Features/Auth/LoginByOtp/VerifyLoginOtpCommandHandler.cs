using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Options;
using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Application.Features.Auth;
using Refahi.Modules.Identity.Application.Features.Auth.Registration;
using Refahi.Modules.Identity.Domain.Repositories;
using Refahi.Shared.Services.Notification;

namespace Refahi.Modules.Identity.Application.Features.Auth.LoginByOtp;

public class VerifyLoginOtpCommandHandler : IRequestHandler<VerifyLoginOtpCommand, VerifyLoginOtpResult>
{
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly IUserRegistrationService _registrationService;
    private readonly IdentityOptions _options;

    public VerifyLoginOtpCommandHandler(
        IUserRepository userRepository,
        INotificationService notificationService,
        IUserRegistrationService registrationService,
        IOptions<IdentityOptions> options)
    {
        _userRepository = userRepository;
        _notificationService = notificationService;
        _registrationService = registrationService;
        _options = options.Value;
    }

    public async Task<VerifyLoginOtpResult> Handle(VerifyLoginOtpCommand request, CancellationToken cancellationToken)
    {
        var otpType = AuthFlow.IsSignUp(request.Flow) ? OtpType.SignUp : OtpType.SignIn;
        var validationResult = await _notificationService.ValidateOtp(request.Token, request.OtpCode, otpType, cancellationToken);

        if (!validationResult.IsValid || string.IsNullOrWhiteSpace(validationResult.Receipt))
            return new VerifyLoginOtpResult(false, "کد OTP نامعتبر یا منقضی شده است");

        if (AuthFlow.IsSignUp(request.Flow))
        {
            if (validationResult.ReceiptType != OtpReceiptType.Sms)
                return new VerifyLoginOtpResult(false, "ثبت‌نام خودکار فقط با شماره موبایل امکان‌پذیر است");

            var registrationResult = await _registrationService.RegisterAsync(
                validationResult.Receipt,
                null,
                cancellationToken);

            if (!registrationResult.Success || registrationResult.User is null)
                return new VerifyLoginOtpResult(false, registrationResult.ErrorMessage);

            var registrationCompleted = _options.QuickRegistrationEnabled;
            return new VerifyLoginOtpResult(
                true,
                null,
                registrationResult.User,
                IsNewUser: true,
                RegistrationCompleted: registrationCompleted,
                ProfileRequired: !registrationCompleted);
        }

        var user = await _userRepository.GetByMobileOrEmailAsync(validationResult.Receipt, cancellationToken);

        if (user is null || !user.IsActive)
            return new VerifyLoginOtpResult(false, "کاربر یافت نشد یا غیرفعال است");

        var userDto = new UserDto(
            user.Id,
            user.MobileNumber,
            user.Email,
            user.IsActive,
            user.GetRoles());

        return new VerifyLoginOtpResult(
            true,
            null,
            userDto,
            IsNewUser: false,
            RegistrationCompleted: true,
            ProfileRequired: false);
    }
}
