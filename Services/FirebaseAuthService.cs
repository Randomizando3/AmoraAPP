using AmoraApp.Config;
using Firebase.Auth;
using Firebase.Auth.Providers;
using Microsoft.Maui.Storage;
using System.Threading.Tasks;

namespace AmoraApp.Services
{
    public class FirebaseAuthService
    {
        private static FirebaseAuthService _instance;
        public static FirebaseAuthService Instance => _instance ??= new FirebaseAuthService();

        private readonly FirebaseAuthClient _client;
        public FirebaseAuthClient Client => _client;

        private FirebaseAuthService()
        {
            var config = new FirebaseAuthConfig
            {
                ApiKey = FirebaseSettings.ApiKey,
                AuthDomain = FirebaseSettings.AuthDomain,
                Providers = new FirebaseAuthProvider[]
                {
                    new EmailProvider(),
                    new GoogleProvider()
                }

                // ❌ NÃO usar UserRepository aqui
                // Isso foi a origem do:
                // - CS0029
                // - sessão trocada
                // - admin errado
            };

            _client = new FirebaseAuthClient(config);
        }

        // =========================
        // AUTH
        // =========================

        public async Task<UserCredential> RegisterWithEmailPasswordAsync(
            string email,
            string password,
            string displayName)
        {
            var credential =
                await _client.CreateUserWithEmailAndPasswordAsync(
                    email,
                    password,
                    displayName);

            return credential;
        }

        public async Task<UserCredential> LoginWithEmailPasswordAsync(
            string email,
            string password)
        {
            var credential =
                await _client.SignInWithEmailAndPasswordAsync(
                    email,
                    password);

            return credential;
        }

        public User GetCurrentUser()
        {
            return _client.User;
        }

        public string CurrentUserUid => _client.User?.Uid;

        public async Task<string> GetIdTokenAsync()
        {
            if (_client.User == null)
                return null;

            // Token é renovado automaticamente pela lib
            return await _client.User.GetIdTokenAsync();
        }

        // =========================
        // LOGOUT SEGURO
        // =========================

        public void Logout()
        {
            try
            {
                _client.SignOut();
            }
            catch
            {
                // ignore
            }

            // Limpa TUDO que pode causar sessão fantasma
            Preferences.Remove("auth_uid");
            Preferences.Remove("is_admin");
            Preferences.Remove("admin_uid");
        }
    }
}
