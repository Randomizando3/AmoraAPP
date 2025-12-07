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

        // Usado para ActivityIndicator, botões, etc.
        [ObservableProperty]
        private bool isBusy;

        // Usado EXCLUSIVAMENTE pelo RefreshView
        [ObservableProperty]
        private bool isRefreshing;

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
            // se já está atualizando, não dispara de novo
            if (IsRefreshing)
                return;

            IsRefreshing = true;   // controla o RefreshView
            IsBusy = true;         // controla ActivityIndicator, botões, etc.
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

                    // Atualiza likes baseado em /postLikes e se eu já dei like
                    var (likes, likedByMe) = await _dbService.GetPostLikesAsync(post.Id, meId);
                    post.Likes = likes;
                    post.LikedByMe = likedByMe;

                    // Carrega 1 ou 2 comentários mais recentes para preview
                    var comments = await _dbService.GetCommentsAsync(post.Id);
                    if (comments != null)
                    {
                        var list = new List<Comment>(comments);
                        list.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
                        if (list.Count > 2)
                            list = list.GetRange(list.Count - 2, 2);

                        post.RecentComments = list;
                    }

                    Posts.Add(post);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                // sempre desliga o spinner e o "busy",
                // mesmo se der erro ou exception.
                IsBusy = false;
                IsRefreshing = false;
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

                // se o amigo não tem story, não mostra a bolha
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

                // recarrega feed (pra já aparecer com avatar, likes, comentários, etc.)
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
        // LIKE EM POST (toggle só no item, sem reload geral)
        // ============================

        [RelayCommand]
        private async Task LikePostAsync(Post post)
        {
            if (post == null) return;

            // tenta obter UID de forma resiliente
            var uid = _authService.CurrentUserUid;
            var user = _authService.GetCurrentUser();
            if (string.IsNullOrEmpty(uid) && user != null)
                uid = user.Uid;

            if (string.IsNullOrEmpty(uid))
            {
                ErrorMessage = "Usuário não logado.";
                return;
            }

            try
            {
                // alterna like no backend
                await _dbService.TogglePostLikeAsync(post.Id, uid);

                // lê só os dados atuais de like desse post
                var (likes, likedByMe) = await _dbService.GetPostLikesAsync(post.Id, uid);
                post.Likes = likes;
                post.LikedByMe = likedByMe;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
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
