using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AmoraApp.Models;
using AmoraApp.Services;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views
{
    public partial class FriendRequestsPage : ContentPage
    {
        private readonly FirebaseAuthService _authService = FirebaseAuthService.Instance;
        private readonly FirebaseDatabaseService _dbService = FirebaseDatabaseService.Instance;
        private readonly FriendService _friendService = FriendService.Instance;

        public ObservableCollection<FriendRequestItem> Requests { get; } = new();

        public FriendRequestsPage()
        {
            InitializeComponent();
            BindingContext = this;
            _ = LoadRequestsAsync();
        }

        private async Task LoadRequestsAsync()
        {
            Requests.Clear();

            var meId = _authService.CurrentUserUid;
            if (string.IsNullOrEmpty(meId))
                return;

            var incomingIds = await _friendService.GetIncomingRequestsAsync(meId);

            foreach (var otherId in incomingIds)
            {
                var profile = await _dbService.GetUserProfileAsync(otherId);
                if (profile == null) continue;

                Requests.Add(new FriendRequestItem
                {
                    UserId = profile.Id,
                    DisplayName = profile.DisplayName,
                    Email = profile.Email,
                    PhotoUrl = profile.PhotoUrl
                });
            }
        }

        private async void OnBackClicked(object sender, System.EventArgs e)
        {
            await Navigation.PopAsync();
        }

        private async void OnAcceptClicked(object sender, System.EventArgs e)
        {
            if (sender is not Button btn || btn.BindingContext is not FriendRequestItem item)
                return;

            var meId = _authService.CurrentUserUid;
            if (string.IsNullOrEmpty(meId))
                return;

            await _friendService.AcceptFriendshipAsync(meId, item.UserId);
            Requests.Remove(item);

            await DisplayAlert("Amizade aceita",
                $"{item.DisplayName} agora é seu amigo. Os stories e posts dele(a) aparecerão no seu feed.",
                "OK");
        }

        private async void OnRejectClicked(object sender, System.EventArgs e)
        {
            if (sender is not Button btn || btn.BindingContext is not FriendRequestItem item)
                return;

            var meId = _authService.CurrentUserUid;
            if (string.IsNullOrEmpty(meId))
                return;

            await _friendService.RejectFriendRequestAsync(meId, item.UserId);
            Requests.Remove(item);
        }
    }

    public class FriendRequestItem
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhotoUrl { get; set; } = string.Empty;
    }
}
