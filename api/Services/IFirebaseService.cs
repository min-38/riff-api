using FirebaseAdmin.Auth;

namespace api.Services;

public interface IFirebaseService
{
    Task<FirebaseToken> VerifyIdTokenAsync(string idToken);
}