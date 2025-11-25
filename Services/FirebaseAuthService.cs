using AmoraApp.Config;
using Firebase.Auth;
using Firebase.Auth.Providers;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace AmoraApp.Services
{
    public class FirebaseAuthService
    {
        // Singleton
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
                    new EmailProvider()
                }
            };

            _client = new FirebaseAuthClient(config);
        }

        public FirebaseAuthClient Client => _client;

        // Agora tenta pegar do cliente; se não tiver, usa o UID salvo em Preferences
        public string? CurrentUserUid
        {
            get
            {
                if (_client.User != null)
                    return _client.User.Uid;

                if (Preferences.ContainsKey(AuthUidKey))
                    return Preferences.Get(AuthUidKey, null);

                return null;
            }
        }

        public User? GetCurrentUser()
        {
            return _client.User;
        }

        public async Task<string?> GetIdTokenAsync()
        {
            var user = _client.User;
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
            {
                // salva UID para auto-login futuro
                Preferences.Set(AuthUidKey, userCredential.User.Uid);
            }

            return userCredential;
        }

        public async Task<UserCredential> LoginWithEmailPasswordAsync(
            string email,
            string password)
        {
            var userCredential = await _client.SignInWithEmailAndPasswordAsync(email, password);

            if (userCredential?.User != null)
            {
                // salva UID para auto-login futuro
                Preferences.Set(AuthUidKey, userCredential.User.Uid);
            }

            return userCredential;
        }

        public void Logout()
        {
            _client.SignOut();

            // limpa o UID salvo (não auto-loga mais)
            Preferences.Remove(AuthUidKey);
        }
    }
}
