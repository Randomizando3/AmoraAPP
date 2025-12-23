using System;

namespace AmoraApp.Services
{
    public static class AdminAccessService
    {
        // UID fixo do admin
        public const string AdminUid = "BMnF5oRKwDfSglunsbtn0kGwxDJ2";

        public static bool IsAdmin(string? uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return false;

            return string.Equals(uid.Trim(), AdminUid, StringComparison.Ordinal);
        }
    }
}
