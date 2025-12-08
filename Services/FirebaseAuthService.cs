using AmoraApp.Config;
using Firebase.Auth;
using Firebase.Auth.Providers;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace AmoraApp.Services
{
    public class FirebaseAuthService
    {
        public static FirebaseAuthService Instance { get; } = new FirebaseAuthService();

        private readonly FirebaseAuthClient _client;

        private const string AuthUidKey = "auth_uid";

        private FirebaseAuthService()
        {
            var config = new FirebaseAuthConfig
            {
                ApiKey = FirebaseSettings.ApiKey,
                AuthDomain = FirebaseSettings.AuthDomain,
                Providers = new FirebaseAuthProvider[]
                {
                    // Habilita Google + Email/senha
                    new GoogleProvider().AddScopes("email"),
                    new EmailProvider()
                }
            };

            _client = new FirebaseAuthClient(config);
        }

        public FirebaseAuthClient Client => _client;

        public string? CurrentUserUid
        {
            get
            {
                if (_client?.User != null)
                    return _client.User.Uid;

                if (Preferences.ContainsKey(AuthUidKey))
                    return Preferences.Get(AuthUidKey, null);

                return null;
            }
        }

        public User? GetCurrentUser()
        {
            return _client?.User;
        }

        public async Task<string?> GetIdTokenAsync()
        {
            var user = _client?.User;
            if (user == null)
                return null;

            return await user.GetIdTokenAsync();
        }

        public async Task<UserCredential> RegisterWithEmailPasswordAsync(
            string email,
            string password,
            string displayName)
        {
            var userCredential = await _client.CreateUserWithEmailAndPasswordAsync(
                email, password, displayName);

            if (userCredential?.User != null)
                Preferences.Set(AuthUidKey, userCredential.User.Uid);

            return userCredential;
        }

        public async Task<UserCredential> LoginWithEmailPasswordAsync(
            string email,
            string password)
        {
            var userCredential = await _client.SignInWithEmailAndPasswordAsync(email, password);

            if (userCredential?.User != null)
                Preferences.Set(AuthUidKey, userCredential.User.Uid);

            return userCredential;
        }

        public void Logout()
        {
            // remove o UID salvo sempre
            Preferences.Remove(AuthUidKey);

            // SE não houver usuário, não chamar SignOut (pois dá NullReference)
            if (_client?.User == null)
                return;

            try
            {
                _client.SignOut();
            }
            catch
            {
                // Se der erro, ignoramos — o logout externo já foi feito
            }
        }
    }
}
