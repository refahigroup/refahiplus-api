using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Options;
using Refahi.Modules.Identity.Application.Features.Auth;
using Refahi.Modules.Identity.Application.Features.Auth.Registration;
using Refahi.Shared.Services.Notification;

namespace Refahi.Modules.Identity.Application.Features.Auth.SignUp;

public class ValidateOtpAndCreateUserCommandHandler : IRequestHandler<ValidateOtpAndCreateUserCommand, ValidateOtpResult>
{
    private readonly INotificationService _notificationService;
    private readonly IUserRegistrationService _registrationService;
    private readonly IdentityOptions _options;

    public ValidateOtpAndCreateUserCommandHandler(
        INotificationService notificationService,
        IUserRegistrationService registrationService,
        IOptions<IdentityOptions> options)
    {
        _notificationService = notificationService;
        _registrationService = registrationService;
        _options = options.Value;
    }

    public async Task<ValidateOtpResult> Handle(ValidateOtpAndCreateUserCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _notificationService.ValidateOtp(request.Token, request.OtpCode, OtpType.SignUp, cancellationToken);

        if (!validationResult.IsValid)
            return new ValidateOtpResult(false, "Invalid or expired OTP code");

        string? mobileNumber = null;
        string? email = null;

        if (validationResult.ReceiptType == OtpReceiptType.Sms)
            mobileNumber = validationResult.Receipt;
        else if (validationResult.ReceiptType == OtpReceiptType.Email)
            email = validationResult.Receipt;

        var registrationResult = await _registrationService.RegisterAsync(mobileNumber, email, cancellationToken);

        if (!registrationResult.Success || registrationResult.User is null)
            return new ValidateOtpResult(false, registrationResult.ErrorMessage);

        var registrationCompleted = _options.QuickRegistrationEnabled;

        return new ValidateOtpResult(
            true,
            null,
            registrationResult.User,
            registrationResult.MobileNumber,
            registrationResult.Email,
            IsNewUser: true,
            RegistrationCompleted: registrationCompleted,
            ProfileRequired: !registrationCompleted);
    }
}
