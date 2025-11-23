using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AmoraApp.Models;
using AmoraApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AmoraApp.ViewModels
{
    public partial class MessagesViewModel : ObservableObject
    {
        private readonly MatchService _matchService;
        private readonly FriendService _friendService;
        private readonly FirebaseDatabaseService _db;
        private readonly FirebaseAuthService _auth;
        private readonly ChatService _chatService;

        [ObservableProperty]
        private bool isBusy;

        /// <summary>
        /// Filtro atual da lista de chats: "All", "Matches", "Friends", "Favorites".
        /// </summary>
        [ObservableProperty]
        private string currentFilter = "All";

        /// <summary>
        /// Lista completa de chats (matches + amigos + conversas com outras pessoas).
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ChatItem> chats = new();

        /// <summary>
        /// Lista final usada na CollectionView de chats, já filtrada e ordenada.
        /// </summary>
        public ObservableCollection<ChatItem> FilteredChats { get; } = new();

        /// <summary>
        /// Carrossel horizontal de matches.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ChatItem> matches = new();

        /// <summary>
        /// Carrossel horizontal de amigos.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ChatItem> friends = new();

        public MessagesViewModel()
            : this(
                MatchService.Instance,
                FriendService.Instance,
                FirebaseDatabaseService.Instance,
                FirebaseAuthService.Instance,
                ChatService.Instance)
        {
        }

        public MessagesViewModel(
            MatchService matchService,
            FriendService friendService,
            FirebaseDatabaseService db,
            FirebaseAuthService auth,
            ChatService chatService)
        {
            _matchService = matchService;
            _friendService = friendService;
            _db = db;
            _auth = auth;
            _chatService = chatService;
        }

        /// <summary>
        /// Carrega matches, amigos e conversa com últimas mensagens reais do Firebase.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;

                var me = _auth.CurrentUserUid;
                if (string.IsNullOrWhiteSpace(me))
                    return;

                // Dicionário para montar apenas 1 ChatItem por usuário
                var map = new Dictionary<string, ChatItem>();

                // =====================================================
                // 1) MATCHES
                // =====================================================
                var matchIds = await _matchService.GetMatchesAsync(me);

                foreach (var otherId in matchIds ?? new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(otherId))
                        continue;

                    if (!map.TryGetValue(otherId, out var chat))
                    {
                        var profile = await _db.GetUserProfileAsync(otherId);
                        if (profile == null)
                            continue;

                        chat = new ChatItem
                        {
                            UserId = profile.Id,
                            DisplayName = profile.DisplayName,
                            PhotoUrl = profile.PhotoUrl ?? string.Empty,
                            IsMatch = true,
                            IsFriend = false,
                            LastMessagePreview = string.Empty,
                            LastMessageAt = DateTime.MinValue,
                            UnreadCount = 0
                        };

                        map[otherId] = chat;
                    }
                    else
                    {
                        chat.IsMatch = true;
                    }
                }

                // =====================================================
                // 2) AMIGOS
                // =====================================================
                var friendIds = await _friendService.GetFriendsAsync(me);

                foreach (var friendId in friendIds ?? new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(friendId) || friendId == me)
                        continue;

                    if (!map.TryGetValue(friendId, out var chat))
                    {
                        var profile = await _db.GetUserProfileAsync(friendId);
                        if (profile == null)
                            continue;

                        chat = new ChatItem
                        {
                            UserId = profile.Id,
                            DisplayName = profile.DisplayName,
                            PhotoUrl = profile.PhotoUrl ?? string.Empty,
                            IsMatch = false,
                            IsFriend = true,
                            LastMessagePreview = string.Empty,
                            LastMessageAt = DateTime.MinValue,
                            UnreadCount = 0
                        };

                        map[friendId] = chat;
                    }
                    else
                    {
                        chat.IsFriend = true;
                    }
                }

                // =====================================================
                // 3) CHATS DO FIREBASE (última mensagem real)
                // =====================================================
                var chatsFromService = await _chatService.GetChatsForUserAsync(me);

                foreach (var chatFromService in chatsFromService)
                {
                    if (string.IsNullOrWhiteSpace(chatFromService.UserId))
                        continue;

                    if (!map.TryGetValue(chatFromService.UserId, out var existing))
                    {
                        // ainda não estava como amigo/match, entra como chat normal
                        map[chatFromService.UserId] = chatFromService;
                    }
                    else
                    {
                        // já é amigo ou match -> mescla informações
                        existing.IsMatch |= chatFromService.IsMatch;
                        existing.IsFavorite |= chatFromService.IsFavorite;

                        if (!string.IsNullOrWhiteSpace(chatFromService.LastMessagePreview))
                            existing.LastMessagePreview = chatFromService.LastMessagePreview;

                        if (chatFromService.LastMessageAt > existing.LastMessageAt)
                            existing.LastMessageAt = chatFromService.LastMessageAt;

                        existing.UnreadCount = chatFromService.UnreadCount;
                    }
                }

                // =====================================================
                // 4) Fallback de placeholder (se ainda não tiver mensagem)
                // =====================================================
                foreach (var c in map.Values)
                {
                    if (string.IsNullOrWhiteSpace(c.LastMessagePreview))
                    {
                        c.LastMessagePreview = c.IsMatch
                            ? "Vocês deram match! 💗"
                            : "Comece a conversar 👋";
                    }

                    if (c.LastMessageAt == DateTime.MinValue)
                        c.LastMessageAt = DateTime.UtcNow;
                }

                // ---------- Monta coleções ----------
                var allChats = map.Values
                    .Where(c => !c.IsBlocked)
                    .ToList();

                Chats = new ObservableCollection<ChatItem>(allChats);

                Matches = new ObservableCollection<ChatItem>(
                    allChats.Where(c => c.IsMatch));

                Friends = new ObservableCollection<ChatItem>(
                    allChats.Where(c => c.IsFriend));

                ApplyOrdering();
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ===================== COMANDOS =====================

        /// <summary>
        /// Filtro: Todos / Matches / Amigos / Favoritos
        /// </summary>
        [RelayCommand]
        private void ChangeFilter(string filterKey)
        {
            if (string.IsNullOrWhiteSpace(filterKey))
                return;

            CurrentFilter = filterKey;
            ApplyOrdering();
        }

        /// <summary>
        /// Marca ou desmarca favorito.
        /// </summary>
        [RelayCommand]
        private async Task ToggleFavorite(ChatItem chat)
        {
            if (chat == null)
                return;

            chat.IsFavorite = !chat.IsFavorite;

            // Se tiver ChatId e userId, salva no Firebase
            if (!string.IsNullOrWhiteSpace(chat.ChatId) && !string.IsNullOrWhiteSpace(chat.UserId))
            {
                var me = _auth.CurrentUserUid;
                if (!string.IsNullOrWhiteSpace(me))
                {
                    await _chatService.SetFavoriteAsync(chat.ChatId, me, chat.IsFavorite);
                }
            }

            ApplyOrdering();
        }

        /// <summary>
        /// Bloqueia o usuário (some das listas).
        /// </summary>
        [RelayCommand]
        private async Task BlockChat(ChatItem chat)
        {
            if (chat == null)
                return;

            chat.IsBlocked = true;

            if (!string.IsNullOrWhiteSpace(chat.ChatId))
            {
                var me = _auth.CurrentUserUid;
                if (!string.IsNullOrWhiteSpace(me))
                {
                    await _chatService.BlockChatAsync(chat.ChatId, me);
                }
            }

            Chats.Remove(chat);
            Matches.Remove(chat);
            Friends.Remove(chat);

            ApplyOrdering();
        }

        /// <summary>
        /// Remove o chat (soft delete pro usuário).
        /// </summary>
        [RelayCommand]
        private async Task DeleteChat(ChatItem chat)
        {
            if (chat == null)
                return;

            if (!string.IsNullOrWhiteSpace(chat.ChatId))
            {
                var me = _auth.CurrentUserUid;
                if (!string.IsNullOrWhiteSpace(me))
                {
                    await _chatService.DeleteChatForUserAsync(chat.ChatId, me);
                }
            }

            Chats.Remove(chat);
            Matches.Remove(chat);
            Friends.Remove(chat);

            ApplyOrdering();
        }

        // ===================== AJUDARES =====================

        private void ApplyOrdering()
        {
            FilteredChats.Clear();

            if (Chats == null || Chats.Count == 0)
                return;

            IEnumerable<ChatItem> source = Chats.Where(c => !c.IsBlocked);

            switch (CurrentFilter)
            {
                case "Matches":
                    source = source.Where(c => c.IsMatch);
                    break;
                case "Friends":
                    source = source.Where(c => c.IsFriend);
                    break;
                case "Favorites":
                    source = source.Where(c => c.IsFavorite);
                    break;
                case "All":
                default:
                    break;
            }

            var ordered = source
                .OrderByDescending(c => c.IsFavorite) // favoritos em cima
                .ThenByDescending(c => c.IsMatch)     // depois matches
                .ThenByDescending(c => c.LastMessageAt);

            foreach (var item in ordered)
                FilteredChats.Add(item);
        }
    }
}
