using System;

namespace Refahi.Modules.Identity.Application.Features.Auth;

public static class AuthFlow
{
    public const string SignIn = "signIn";
    public const string SignUp = "signUp";

    public static bool IsSignIn(string? flow) =>
        string.Equals(flow, SignIn, StringComparison.OrdinalIgnoreCase);

    public static bool IsSignUp(string? flow) =>
        string.Equals(flow, SignUp, StringComparison.OrdinalIgnoreCase);
}
