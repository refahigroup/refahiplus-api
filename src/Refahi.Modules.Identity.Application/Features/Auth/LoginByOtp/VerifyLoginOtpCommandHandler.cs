using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Domain.Repositories;
using Refahi.Shared.Services.Notification;

namespace Refahi.Modules.Identity.Application.Features.Auth.LoginByOtp;

public class VerifyLoginOtpCommandHandler : IRequestHandler<VerifyLoginOtpCommand, VerifyLoginOtpResult>
{
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;

    public VerifyLoginOtpCommandHandler(
        IUserRepository userRepository,
        INotificationService notificationService)
    {
        _userRepository = userRepository;
        _notificationService = notificationService;
    }

    public async Task<VerifyLoginOtpResult> Handle(VerifyLoginOtpCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _notificationService.ValidateOtp(request.Token, request.OtpCode, OtpType.SignIn, cancellationToken);

        if (!validationResult.IsValid || string.IsNullOrWhiteSpace(validationResult.Receipt))
            return new VerifyLoginOtpResult(false, "کد OTP نامعتبر یا منقضی شده است");

        var user = await _userRepository.GetByMobileOrEmailAsync(validationResult.Receipt, cancellationToken);

        if (user is null || !user.IsActive)
            return new VerifyLoginOtpResult(false, "کاربر یافت نشد یا غیرفعال است");

        var userDto = new UserDto(
            user.Id,
            user.MobileNumber,
            user.Email,
            user.IsActive,
            user.GetRoles());

        return new VerifyLoginOtpResult(true, null, userDto);
    }
}
