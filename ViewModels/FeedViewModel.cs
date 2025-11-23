using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AmoraApp.Models;
using AmoraApp.Services;
using AmoraApp.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui;

namespace AmoraApp.ViewModels
{
    public partial class FeedViewModel : ObservableObject
    {
        private readonly FirebaseDatabaseService _dbService;
        private readonly FirebaseAuthService _authService;
        private readonly MatchService _matchService;
        private readonly FriendService _friendService;

        public ObservableCollection<Post> Posts { get; } = new();
        public ObservableCollection<StoryBubble> Stories { get; } = new();

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string errorMessage = string.Empty;

        [ObservableProperty]
        private string newPostText = string.Empty;

        [ObservableProperty]
        private string newPostImageUrl = string.Empty; // url da imagem do novo post

        [ObservableProperty]
        private int pendingRequestsCount; // badge no header

        // Construtor sem parâmetros (se o XAML precisar)
        public FeedViewModel()
            : this(
                  FirebaseDatabaseService.Instance,
                  FirebaseAuthService.Instance,
                  MatchService.Instance)
        {
        }

        public FeedViewModel(
            FirebaseDatabaseService dbService,
            FirebaseAuthService authService,
            MatchService matchService)
        {
            _dbService = dbService;
            _authService = authService;
            _matchService = matchService;
            _friendService = FriendService.Instance;
        }

        // ============================
        // CARREGAR FEED + STORIES + SOLICITAÇÕES
        // ============================

        [RelayCommand]
        private async Task LoadFeedAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                Posts.Clear();
                Stories.Clear();

                var meId = _authService.CurrentUserUid;
                if (string.IsNullOrEmpty(meId))
                {
                    ErrorMessage = "Usuário não logado.";
                    return;
                }

                // 0) Contador de solicitações pendentes
                var incoming = await _friendService.GetIncomingRequestsAsync(meId);
                PendingRequestsCount = incoming?.Count ?? 0;

                // 1) Montar bolhas de stories com base em amigos
                await BuildStoriesAsync(meId);

                // 2) Carregar posts (apenas meus + amigos)
                var allPosts = await _dbService.GetRecentPostsAsync();

                var friendIds = await _friendService.GetFriendsAsync(meId);
                var allowedIds = new HashSet<string>(friendIds) { meId };

                foreach (var post in allPosts)
                {
                    if (!allowedIds.Contains(post.UserId))
                        continue;

                    // Garante que a foto de quem postou esteja preenchida
                    if (string.IsNullOrEmpty(post.UserPhotoUrl))
                    {
                        var p = await _dbService.GetUserProfileAsync(post.UserId);
                        post.UserPhotoUrl = p?.PhotoUrl ?? string.Empty;
                    }

                    // Atualiza likes baseado em /postLikes
                    var (likes, _) = await _dbService.GetPostLikesAsync(post.Id, meId);
                    post.Likes = likes;

                    Posts.Add(post);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task BuildStoriesAsync(string meId)
        {
            // -------------------------
            // MINHA BOLHA ("VOCÊ")
            // -------------------------
            var myProfile = await _dbService.GetUserProfileAsync(meId);
            var myStories = await StoryService.Instance.GetStoriesAsync(meId);
            var myPreview = myStories.Count > 0 ? myStories[^1].ImageUrl : string.Empty;

            Stories.Add(new StoryBubble
            {
                UserId = meId,
                UserName = myProfile?.DisplayName ?? "Você",
                PhotoUrl = myProfile?.PhotoUrl ?? string.Empty,
                PreviewImageUrl = myPreview,
                IsMe = true,
                IsMatch = false
            });

            // -------------------------
            // AMIGOS (incluindo matches, porque match → friend)
            // -------------------------
            var friendIds = await _friendService.GetFriendsAsync(meId);

            // matches do usuário, para marcar coraçãozinho nas bolhas
            var myMatches = await _matchService.GetMatchesAsync(meId);
            var matchSet = new HashSet<string>(myMatches);

            foreach (var friendId in friendIds)
            {
                var profile = await _dbService.GetUserProfileAsync(friendId);
                if (profile == null)
                    continue;

                var friendStories = await StoryService.Instance.GetStoriesAsync(friendId);

                // 👉 se o amigo não tem story, NÃO mostra a bolha
                if (friendStories == null || friendStories.Count == 0)
                    continue;

                var preview = friendStories[^1].ImageUrl;

                Stories.Add(new StoryBubble
                {
                    UserId = friendId,
                    UserName = profile.DisplayName,
                    PhotoUrl = profile.PhotoUrl,
                    PreviewImageUrl = preview,
                    IsMe = false,
                    IsMatch = matchSet.Contains(friendId) // coração se for match
                });
            }
        }

        // ============================
        // NOVO POST
        // ============================

        [RelayCommand]
        private async Task PublishPostAsync()
        {
            if (IsBusy) return;

            var text = NewPostText?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(NewPostImageUrl))
                return; // não posta nada vazio

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                var user = _authService.GetCurrentUser();
                if (user == null)
                {
                    ErrorMessage = "Usuário não logado.";
                    return;
                }

                var profile = await _dbService.GetUserProfileAsync(user.Uid);

                var post = new Post
                {
                    UserId = user.Uid,
                    UserName = profile?.DisplayName ?? user.Info.DisplayName ?? user.Info.Email,
                    UserPhotoUrl = profile?.PhotoUrl ?? string.Empty,
                    Text = text,
                    ImageUrl = NewPostImageUrl ?? string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    Likes = 0
                };

                await _dbService.CreatePostAsync(post);

                // limpa campos
                NewPostText = string.Empty;
                NewPostImageUrl = string.Empty;

                // recarrega feed (pra já aparecer com avatar)
                await LoadFeedAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ============================
        // AÇÕES DE UI
        // ============================

        [RelayCommand]
        private async Task OpenFriendRequestsAsync()
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await App.Current.MainPage.Navigation.PushAsync(new FriendRequestsPage());
            });
        }
    }
}
