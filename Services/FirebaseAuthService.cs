using AmoraApp.Config;
using Firebase.Auth;
using Firebase.Auth.Providers;
using System.Threading.Tasks;

namespace AmoraApp.Services
{
    public class FirebaseAuthService
    {
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
                    new GoogleProvider().AddScopes("email"),
                    new EmailProvider()
                }
            };

            _client = new FirebaseAuthClient(config);
        }

        public FirebaseAuthClient Client => _client;

        // UID somente se houver sessão real
        public string? CurrentUserUid => _client?.User?.Uid;

        public User? GetCurrentUser() => _client?.User;

        public async Task<string?> GetIdTokenAsync()
        {
            var user = _client?.User;
            if (user == null)
                return null;

            return await user.GetIdTokenAsync();
        }

        public Task<UserCredential> RegisterWithEmailPasswordAsync(string email, string password, string displayName)
            => _client.CreateUserWithEmailAndPasswordAsync(email, password, displayName);

        public Task<UserCredential> LoginWithEmailPasswordAsync(string email, string password)
            => _client.SignInWithEmailAndPasswordAsync(email, password);

        public void Logout()
        {
            if (_client?.User == null)
                return;

            try { _client.SignOut(); } catch { }
        }
    }
}
