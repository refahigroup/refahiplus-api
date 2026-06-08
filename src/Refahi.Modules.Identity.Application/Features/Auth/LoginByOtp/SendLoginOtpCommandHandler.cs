using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Options;
using Refahi.Modules.Identity.Application.Features.Auth;
using Refahi.Modules.Identity.Domain.Repositories;
using Refahi.Shared.Services.Notification;

namespace Refahi.Modules.Identity.Application.Features.Auth.LoginByOtp;

public class SendLoginOtpCommandHandler : IRequestHandler<SendLoginOtpCommand, SendLoginOtpResult>
{
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly IdentityOptions _options;

    public SendLoginOtpCommandHandler(
        IUserRepository userRepository,
        INotificationService notificationService,
        IOptions<IdentityOptions> options)
    {
        _userRepository = userRepository;
        _notificationService = notificationService;
        _options = options.Value;
    }

    public async Task<SendLoginOtpResult> Handle(SendLoginOtpCommand request, CancellationToken cancellationToken)
    {
        OtpReceiptType receiptType;
        bool userExists;

        if (Regex.IsMatch(request.Contact, @"^09\d{9}$"))
        {
            receiptType = OtpReceiptType.Sms;
            userExists = await _userRepository.ExistsByMobileNumberAsync(request.Contact, cancellationToken);
        }
        else
        {
            receiptType = OtpReceiptType.Email;
            userExists = await _userRepository.ExistsByEmailAsync(request.Contact, cancellationToken);
        }

        if (!userExists)
        {
            if (receiptType != OtpReceiptType.Sms)
                return new SendLoginOtpResult(false, "کاربری با این ایمیل یافت نشد");

            if (!_options.AutoRegistrationEnabled)
                return new SendLoginOtpResult(false, "کاربری با این شماره موبایل یافت نشد");

            var signUpOtpResult = await _notificationService.SendOtp(request.Contact, receiptType, OtpType.SignUp, cancellationToken);
            return new SendLoginOtpResult(
                true,
                null,
                signUpOtpResult.ReferenceCode,
                signUpOtpResult.ExpiresAt,
                AuthFlow.SignUp);
        }

        var otpResult = await _notificationService.SendOtp(request.Contact, receiptType, OtpType.SignIn, cancellationToken);

        return new SendLoginOtpResult(true, null, otpResult.ReferenceCode, otpResult.ExpiresAt, AuthFlow.SignIn);
    }
}
