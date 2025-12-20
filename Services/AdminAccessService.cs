using AmoraApp.Config;

namespace AmoraApp.Services
{
    public static class AdminAccessService
    {
        public static bool IsAdmin(string? uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return false;

            return FirebaseSettings.AdminUids.Contains(uid);
        }
    }
}
