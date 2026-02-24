namespace Refahi.Modules.Identity.Domain.Exceptions;

public class DomainException : Exception
{
    public string ErrorCode { get; }

    public DomainException(string message, string errorCode = "DOMAIN_ERROR")
        : base(message)
    {
        ErrorCode = errorCode;
    }
}

public class UserNotFoundException : DomainException
{
    public UserNotFoundException(Guid userId)
        : base($"User with ID {userId} not found", "USER_NOT_FOUND")
    {
    }
}

public class UserAlreadyExistsException : DomainException
{
    public UserAlreadyExistsException(string mobileOrEmail)
        : base($"User with {mobileOrEmail} already exists", "USER_ALREADY_EXISTS")
    {
    }
}

public class InvalidOtpException : DomainException
{
    public InvalidOtpException()
        : base("The OTP code is invalid", "INVALID_OTP")
    {
    }
}

public class OtpExpiredException : DomainException
{
    public OtpExpiredException()
        : base("The OTP code has expired", "OTP_EXPIRED")
    {
    }
}

public class WeakPasswordException : DomainException
{
    public WeakPasswordException()
        : base("Password must be at least 8 characters", "WEAK_PASSWORD")
    {
    }
}

public class ProfileAlreadyExistsException : DomainException
{
    public ProfileAlreadyExistsException(Guid userId)
        : base($"Profile already exists for user {userId}", "PROFILE_ALREADY_EXISTS")
    {
    }
}

public class ProfileNotFoundException : DomainException
{
    public ProfileNotFoundException(Guid userId)
        : base($"Profile not found for user {userId}", "PROFILE_NOT_FOUND")
    {
    }
}
