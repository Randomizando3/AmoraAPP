namespace AmoraApp.Config
{
    public static class FirebaseSettings
    {
        // SUBSTITUA PELOS DADOS DO SEU PROJETO FIREBASE (mantive os seus)
        public const string ApiKey = "AIzaSyC2JyanjRPU2RpRenNz-BVlxr8XwTwNJKg";
        public const string AuthDomain = "amora-app-dev.firebaseapp.com";
        public const string DatabaseUrl = "https://amora-app-dev-default-rtdb.firebaseio.com/";
        public static string StorageBucket = "amora-app-dev.firebasestorage.app";

        // ===== ADMIN =====
        // UID(s) que podem acessar o painel admin
        public static readonly HashSet<string> AdminUids = new HashSet<string>
        {
            "KQthqzhcHeVVeAOCdWUFvEwrAfA2"
        };
    }
}
