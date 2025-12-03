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
        /// Lista completa de chats (matches + amigos + conversas com outras pessoas + grupos).
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ChatItem> chats = new();

        /// <summary>
        /// Lista final usada na CollectionView de chats, já filtrada e ordenada.
        /// </summary>
        public ObservableCollection<ChatItem> FilteredChats { get; } = new();

        /// <summary>
        /// Carrossel horizontal de matches (somente 1x1).
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ChatItem> matches = new();

        /// <summary>
        /// Carrossel horizontal de amigos (somente 1x1).
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
        /// Carrega matches, amigos e conversas (incluindo grupos) com últimas mensagens reais do Firebase.
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

                // Dicionário para montar apenas 1 ChatItem por usuário (apenas 1x1)
                var mapByUser = new Dictionary<string, ChatItem>();

                // Lista separada para grupos
                var groupChats = new List<ChatItem>();

                // =====================================================
                // 1) MATCHES (1x1)
                // =====================================================
                var matchIds = await _matchService.GetMatchesAsync(me);

                foreach (var otherId in matchIds ?? new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(otherId))
                        continue;

                    if (!mapByUser.TryGetValue(otherId, out var chat))
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
                            UnreadCount = 0,
                            IsGroup = false
                        };

                        mapByUser[otherId] = chat;
                    }
                    else
                    {
                        chat.IsMatch = true;
                    }
                }

                // =====================================================
                // 2) AMIGOS (1x1)
                // =====================================================
                var friendIds = await _friendService.GetFriendsAsync(me);

                foreach (var friendId in friendIds ?? new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(friendId) || friendId == me)
                        continue;

                    if (!mapByUser.TryGetValue(friendId, out var chat))
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
                            UnreadCount = 0,
                            IsGroup = false
                        };

                        mapByUser[friendId] = chat;
                    }
                    else
                    {
                        chat.IsFriend = true;
                    }
                }

                // =====================================================
                // 3) CHATS DO FIREBASE (1x1 + GRUPOS)
                // =====================================================
                var chatsFromService = await _chatService.GetChatsForUserAsync(me);

                foreach (var chatFromService in chatsFromService)
                {
                    if (chatFromService == null)
                        continue;

                    // ---- GRUPO ----
                    if (chatFromService.IsGroup)
                    {
                        // garante que não duplica
                        if (groupChats.All(g => g.ChatId != chatFromService.ChatId))
                        {
                            groupChats.Add(chatFromService);
                        }
                        continue;
                    }

                    // ---- 1x1 ----
                    if (string.IsNullOrWhiteSpace(chatFromService.UserId))
                        continue;

                    if (!mapByUser.TryGetValue(chatFromService.UserId, out var existing))
                    {
                        // ainda não estava como amigo/match, entra como chat normal
                        mapByUser[chatFromService.UserId] = chatFromService;
                    }
                    else
                    {
                        // já é amigo ou match -> mescla informações
                        existing.IsMatch |= chatFromService.IsMatch;
                        existing.IsFriend |= chatFromService.IsFriend;
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
                //    Aplica para 1x1 e grupos
                // =====================================================
                foreach (var c in mapByUser.Values.Concat(groupChats))
                {
                    if (string.IsNullOrWhiteSpace(c.LastMessagePreview))
                    {
                        if (!c.IsGroup)
                        {
                            c.LastMessagePreview = c.IsMatch
                                ? "Vocês deram match! 💗"
                                : "Comece a conversar 👋";
                        }
                        else
                        {
                            c.LastMessagePreview = "Novo grupo criado. Diga um oi! 👋";
                        }
                    }

                    if (c.LastMessageAt == DateTime.MinValue)
                        c.LastMessageAt = DateTime.UtcNow;
                }

                // ---------- Monta coleções ----------
                var allChats = mapByUser.Values
                    .Concat(groupChats)
                    .Where(c => !c.IsBlocked)
                    .ToList();

                Chats = new ObservableCollection<ChatItem>(allChats);

                // Carrossel de matches: apenas 1x1
                Matches = new ObservableCollection<ChatItem>(
                    allChats.Where(c => !c.IsGroup && c.IsMatch));

                // Carrossel de amigos: apenas 1x1
                Friends = new ObservableCollection<ChatItem>(
                    allChats.Where(c => !c.IsGroup && c.IsFriend));

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

            // Se tiver ChatId, salva no Firebase (grupo ou 1x1)
            if (!string.IsNullOrWhiteSpace(chat.ChatId))
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
        /// Bloqueia o usuário / grupo (some das listas).
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
        /// OBS: para grupos, a lógica de sair/excluir grupo está no code-behind da ChatListPage.
        /// Aqui é usado principalmente para chats 1x1.
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
                    // Só 1x1 com match
                    source = source.Where(c => !c.IsGroup && c.IsMatch);
                    break;

                case "Friends":
                    // Só 1x1 amigos
                    source = source.Where(c => !c.IsGroup && c.IsFriend);
                    break;

                case "Favorites":
                    // Favoritos (1x1 + grupos)
                    source = source.Where(c => c.IsFavorite);
                    break;

                case "All":
                default:
                    // Todos (1x1 + grupos)
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
