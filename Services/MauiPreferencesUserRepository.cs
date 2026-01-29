// Services/MauiPreferencesUserRepository.cs
using System;
using System.Text.Json;
using System.Threading.Tasks;
using AmoraApp.Models;
using Microsoft.Maui.Storage;

namespace AmoraApp.Services
{
    public sealed class MauiPreferencesUserRepository
    {
        private const string Key = "amora_user_session_v1";

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public Task SaveAsync(UserDal user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            user.SavedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var json = JsonSerializer.Serialize(user, JsonOpts);
            Preferences.Set(Key, json);

            return Task.CompletedTask;
        }

        public Task<UserDal?> LoadAsync()
        {
            try
            {
                var json = Preferences.Get(Key, string.Empty);
                if (string.IsNullOrWhiteSpace(json))
                    return Task.FromResult<UserDal?>(null);

                var user = JsonSerializer.Deserialize<UserDal>(json, JsonOpts);
                if (user == null || string.IsNullOrWhiteSpace(user.Uid))
                    return Task.FromResult<UserDal?>(null);

                return Task.FromResult<UserDal?>(user);
            }
            catch
            {
                // Se corromper o JSON, limpa e segue sem sessão
                Preferences.Remove(Key);
                return Task.FromResult<UserDal?>(null);
            }
        }

        public Task ClearAsync()
        {
            Preferences.Remove(Key);
            return Task.CompletedTask;
        }

        public Task SaveUidOnlyAsync(string uid)
        {
            var dal = new UserDal
            {
                Uid = uid ?? string.Empty,
                IsAdmin = false
            };

            return SaveAsync(dal);
        }
    }
}
