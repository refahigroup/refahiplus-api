using MediatR;
using Refahi.Shared.Services.Notification;
using System.Threading;
using System.Threading.Tasks;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.Auth.SignUp;

public class SendOtpCommandHandler : IRequestHandler<SendOtpCommand, SendOtpResult>
{
    private readonly INotificationService _notificationService;
    private readonly IUserRepository _userRepository;

    public SendOtpCommandHandler(
        INotificationService notificationService,
        IUserRepository userRepository)
    {
        _notificationService = notificationService;
        _userRepository = userRepository;
    }

    public async Task<SendOtpResult> Handle(SendOtpCommand request, CancellationToken cancellationToken)
    {
        // Note: Basic validation (mobile/email format, null checks) is handled by FluentValidation pipeline
        
        // Determine receipt and type
        string receipt;
        OtpReceiptType receiptType;

        if (!string.IsNullOrWhiteSpace(request.MobileNumber))
        {
            receipt = request.MobileNumber;
            receiptType = OtpReceiptType.Sms;

            // Check if user with this mobile number already exists
            if (await _userRepository.ExistsByMobileNumberAsync(receipt, cancellationToken))
            {
                return new SendOtpResult(false, "User with this mobile number already exists");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.Email))
        {
            receipt = request.Email;
            receiptType = OtpReceiptType.Email;

            // Check if user with this email already exists
            if (await _userRepository.ExistsByEmailAsync(receipt, cancellationToken))
            {
                return new SendOtpResult(false, "User with this email already exists");
            }
        }
        else
        {
            return new SendOtpResult(false, "Either mobile number or email is required");
        }

        // Send OTP via notification service (it handles generation, storage, and sending)
        var otpResult = await _notificationService.SendOtp(receipt, receiptType, OtpType.SignUp, cancellationToken);

        return new SendOtpResult(true, null, otpResult.ReferenceCode, otpResult.ExpiresAt);
    }
}
