namespace DentalPro.Application.Identity;

public sealed record AuthenticationAttempt
{
    private AuthenticationAttempt(AuthenticatedSession? session, AuthenticationFailure? failure)
    {
        Session = session;
        Failure = failure;
    }

    public AuthenticatedSession? Session { get; }

    public AuthenticationFailure? Failure { get; }

    public bool Succeeded => Session is not null;

    public static AuthenticationAttempt Success(AuthenticatedSession session) =>
        new(session, null);

    public static AuthenticationAttempt Rejected(AuthenticationFailure failure) =>
        new(null, failure);
}
