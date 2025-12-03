using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;

namespace api.Services;

public class FirebaseService : IFirebaseService
{
    private readonly ILogger<FirebaseService> _logger;

    public FirebaseService(ILogger<FirebaseService> logger, IConfiguration configuration)
    {
        _logger = logger;

        // Initialize Firebase App if not already initialized
        if (FirebaseApp.DefaultInstance == null)
        {
            // Assuming GOOGLE_APPLICATION_CREDENTIALS environment variable is set
            // or we can load from a file path in appsettings
            var credentialPath = configuration["Firebase:CredentialPath"];

            if (!string.IsNullOrEmpty(credentialPath))
            {
                FirebaseApp.Create(new AppOptions()
                {
                    Credential = GoogleCredential.FromFile(credentialPath)
                });
            }
            else
            {
                // Fallback to default application credentials (e.g. environment variable)
                FirebaseApp.Create(new AppOptions()
                {
                    Credential = GoogleCredential.GetApplicationDefault()
                });
            }
        }
    }

    public async Task<FirebaseToken> VerifyIdTokenAsync(string idToken)
    {
        try
        {
            // Verify the ID token while checking if the token is revoked by passing checkRevoked
            // as true.
            var decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken, checkRevoked: true);
            return decodedToken;
        }
        catch (FirebaseAuthException ex)
        {
            _logger.LogError(ex, "Error verifying Firebase ID token");
            throw new InvalidOperationException("Invalid Firebase ID token", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error verifying Firebase ID token");
            throw;
        }
    }
}
