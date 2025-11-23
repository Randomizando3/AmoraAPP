using AmoraApp.Config;
using Firebase.Auth;
using Firebase.Auth.Providers;
using System.Threading.Tasks;

namespace AmoraApp.Services
{
    public class FirebaseAuthService
    {
        // Singleton simples
        public static FirebaseAuthService Instance { get; } = new FirebaseAuthService();

        private readonly FirebaseAuthClient _client;

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

        // ✅ Propriedade que estava faltando
        public string? CurrentUserUid => _client.User?.Uid;

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

            return userCredential;
        }

        public async Task<UserCredential> LoginWithEmailPasswordAsync(
            string email,
            string password)
        {
            var userCredential = await _client.SignInWithEmailAndPasswordAsync(email, password);
            return userCredential;
        }

        public void Logout()
        {
            _client.SignOut();
        }
    }
}
