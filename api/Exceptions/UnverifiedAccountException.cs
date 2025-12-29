namespace api.Exceptions;

// 미인증 계정 예외
public class UnverifiedAccountException : Exception
{
    public string VerificationToken { get; }
    public int? RemainingCooldown { get; }

    public UnverifiedAccountException(string message, string verificationToken, int? remainingCooldown = null)
        : base(message)
    {
        VerificationToken = verificationToken;
        RemainingCooldown = remainingCooldown;
    }
}
