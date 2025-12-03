using AmoraApp.Models;
using AmoraApp.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AmoraApp.Views
{
    public partial class ChatPage : ContentPage
    {
        private ChatItem? _chatItem;

        public string OtherUserId { get; private set; } = string.Empty;
        public string OtherUserName { get; private set; } = "Contato";

        private string _chatId = string.Empty;

        private bool _isGroupChat;
        private bool _isGroupAdmin;
        private string _groupName = string.Empty;

        // Agora pega o UID diretamente do FirebaseAuthService
        private string CurrentUserId =>
            FirebaseAuthService.Instance.CurrentUserUid ?? string.Empty;

        // ===================== CONSTRUTORES =====================

        /// <summary>
        /// Construtor principal: recebe um ChatItem (pode ser 1x1 ou grupo).
        /// </summary>
        public ChatPage(ChatItem chat)
        {
            _chatItem = chat ?? throw new ArgumentNullException(nameof(chat));

            InitializeComponent();

            _isGroupChat = chat.IsGroup;
            _isGroupAdmin = chat.IsGroupAdmin;
            _groupName = chat.GroupName ?? string.Empty;

            _chatId = chat.ChatId;

            if (!_isGroupChat)
            {
                OtherUserId = chat.UserId ?? string.Empty;
                OtherUserName = string.IsNullOrWhiteSpace(chat.DisplayName) ? "Contato" : chat.DisplayName;
            }
            else
            {
                OtherUserId = string.Empty;
                OtherUserName = string.IsNullOrWhiteSpace(chat.DisplayName)
                    ? $"Grupo {_groupName}"
                    : chat.DisplayName;
            }

            UpdateHeader();
        }

        /// <summary>
        /// Construtor de conveniência para 1x1, mantido para compatibilidade.
        /// </summary>
        public ChatPage(string otherUserId, string otherUserName)
            : this(new ChatItem
            {
                ChatId = string.Empty,
                UserId = otherUserId,
                DisplayName = otherUserName,
                IsGroup = false
            })
        {
        }

        /// <summary>
        /// Construtor sem parâmetros – mantém por compatibilidade (Preview/XAML).
        /// </summary>
        public ChatPage()
            : this(new ChatItem
            {
                ChatId = string.Empty,
                UserId = string.Empty,
                DisplayName = "Contato",
                IsGroup = false
            })
        {
        }

        private void UpdateHeader()
        {
            if (_isGroupChat)
            {
                var name = !string.IsNullOrWhiteSpace(_groupName)
                    ? _groupName
                    : OtherUserName;

                UserNameLabel.Text = $"Grupo {name}";

                if (_chatItem != null && _chatItem.MembersCount > 0)
                    StatusLabel.Text = $"{_chatItem.MembersCount} membros";
                else
                    StatusLabel.Text = "Grupo";

                StatusLabel.TextColor = Colors.Gray;
            }
            else
            {
                UserNameLabel.Text = OtherUserName;
                StatusLabel.Text = "Online";
                StatusLabel.TextColor = Color.FromArgb("#5d259c");
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (string.IsNullOrWhiteSpace(CurrentUserId))
            {
                Console.WriteLine("[ChatPage] CurrentUserId vazio.");
                return;
            }

            if (_isGroupChat)
            {
                if (string.IsNullOrWhiteSpace(_chatId))
                {
                    await DisplayAlert("Erro",
                        "Grupo inválido.",
                        "OK");
                    return;
                }

                await LoadMessagesAsync();
                // não tem presença em grupo
                return;
            }

            // 1x1
            if (string.IsNullOrWhiteSpace(OtherUserId))
            {
                Console.WriteLine("[ChatPage] OtherUserId inválido no chat 1x1.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_chatId))
            {
                try
                {
                    _chatId = await ChatService.Instance.GetOrCreateChatAsync(CurrentUserId, OtherUserId);
                    Console.WriteLine($"[ChatPage] ChatId obtido/criado: '{_chatId}'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ChatPage] Erro ao obter chatId: {ex}");
                    await DisplayAlert("Erro",
                        "Não foi possível iniciar a conversa. Tente novamente.",
                        "OK");
                    return;
                }
            }

            await LoadMessagesAsync();
            await UpdateOtherUserStatusAsync();
        }

        private async Task LoadMessagesAsync()
        {
            if (string.IsNullOrWhiteSpace(_chatId))
                return;

            var list = await ChatService.Instance.GetMessagesAsync(_chatId, 80);

            MessagesStack.Children.Clear();

            foreach (var msg in list)
            {
                AddBubble(msg);
            }

            ScrollToBottom();

            // marca como lidas
            try
            {
                await ChatService.Instance.MarkMessagesAsReadAsync(_chatId, CurrentUserId, list);
            }
            catch { }
        }

        // ===== PRESENÇA DO OUTRO USUÁRIO (apenas 1x1) =====

        private async Task UpdateOtherUserStatusAsync()
        {
            if (_isGroupChat)
                return;

            if (string.IsNullOrWhiteSpace(OtherUserId))
                return;

            try
            {
                var presence = await PresenceService.Instance.GetPresenceAsync(OtherUserId);
                if (presence == null)
                {
                    StatusLabel.Text = string.Empty;
                    return;
                }

                var (isOnline, lastSeenUtc) = presence.Value;

                if (isOnline)
                {
                    StatusLabel.Text = "Online";
                    StatusLabel.TextColor = Color.FromArgb("#5d259c");
                }
                else
                {
                    if (lastSeenUtc <= 0)
                    {
                        StatusLabel.Text = "Offline";
                        StatusLabel.TextColor = Colors.Gray;
                        return;
                    }

                    var lastSeen = DateTimeOffset.FromUnixTimeSeconds(lastSeenUtc).LocalDateTime;
                    var diff = DateTime.Now - lastSeen;

                    string text;
                    if (diff.TotalMinutes < 1)
                        text = "Visto agora";
                    else if (diff.TotalHours < 1)
                        text = $"Visto há {(int)diff.TotalMinutes} min";
                    else if (diff.TotalDays < 1)
                        text = $"Visto há {(int)diff.TotalHours} h";
                    else
                        text = $"Visto em {lastSeen:dd/MM HH:mm}";

                    StatusLabel.Text = text;
                    StatusLabel.TextColor = Colors.Gray;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatPage] Erro ao atualizar status online: {ex}");
            }
        }

        // ===== EVENTOS DO INPUT =====

        private async void OnEntryCompleted(object sender, EventArgs e)
        {
            await SendCurrentMessageAsync();
        }

        private async void OnSendTapped(object sender, TappedEventArgs e)
        {
            await SendCurrentMessageAsync();
        }

        // Clique no botão de imagem
        private async void OnAttachImageClicked(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentUserId))
                {
                    await DisplayAlert("Erro",
                        "Usuário atual não identificado. Faça login novamente.",
                        "OK");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_chatId))
                {
                    if (_isGroupChat)
                    {
                        await DisplayAlert("Erro",
                            "Grupo inválido.",
                            "OK");
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(OtherUserId))
                    {
                        await DisplayAlert("Erro",
                            "Contato inválido para esta conversa.",
                            "OK");
                        return;
                    }

                    _chatId = await ChatService.Instance.GetOrCreateChatAsync(CurrentUserId, OtherUserId);
                    Console.WriteLine($"[ChatPage] ChatId criado no envio de imagem: '{_chatId}'");
                }

                var pickResult = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Selecione uma imagem",
                    FileTypes = FilePickerFileType.Images
                });

                if (pickResult == null)
                    return;

                using var stream = await pickResult.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var bytes = ms.ToArray();
                var base64 = Convert.ToBase64String(bytes);

                var msg = new ChatMessage
                {
                    ChatId = _chatId,
                    SenderId = CurrentUserId,
                    Text = string.Empty,
                    ImageBase64 = base64,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ReadBy = new Dictionary<string, bool>
                    {
                        [CurrentUserId] = true
                    }
                };

                await ChatService.Instance.SendMessageAsync(_chatId, msg);

                AddBubble(msg);
                ScrollToBottom();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatPage] Erro ao anexar imagem: {ex}");
                await DisplayAlert("Erro",
                    "Não foi possível enviar a imagem.",
                    "OK");
            }
        }

        // Clique no botão de áudio (upload de arquivo de áudio)
        private async void OnAttachAudioClicked(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentUserId))
                {
                    await DisplayAlert("Erro",
                        "Usuário atual não identificado. Faça login novamente.",
                        "OK");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_chatId))
                {
                    if (_isGroupChat)
                    {
                        await DisplayAlert("Erro",
                            "Grupo inválido.",
                            "OK");
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(OtherUserId))
                    {
                        await DisplayAlert("Erro",
                            "Contato inválido para esta conversa.",
                            "OK");
                        return;
                    }

                    _chatId = await ChatService.Instance.GetOrCreateChatAsync(CurrentUserId, OtherUserId);
                    Console.WriteLine($"[ChatPage] ChatId criado no envio de áudio: '{_chatId}'");
                }

                var customAudioType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "audio/mpeg", "audio/mp3", "audio/aac", "audio/ogg", "audio/x-wav", "audio/wav" } },
                    { DevicePlatform.iOS,     new[] { "public.audio" } },
                    { DevicePlatform.WinUI,   new[] { ".mp3", ".wav", ".aac", ".ogg" } },
                    { DevicePlatform.MacCatalyst, new[] { "public.audio" } }
                });

                var pickResult = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Selecione um áudio",
                    FileTypes = customAudioType
                });

                if (pickResult == null)
                    return;

                await using var fileStream = await pickResult.OpenReadAsync();

                var safeName = pickResult.FileName.Replace(" ", "_");
                var fileName = $"chatAudio/{_chatId}/{Guid.NewGuid():N}_{safeName}";

                // usando método genérico de upload (ajuste se o nome for diferente)
                var audioUrl = await FirebaseStorageService.Instance.UploadFileAsync(
                    fileStream,
                    fileName,
                    pickResult.ContentType ?? "audio/mpeg");

                if (string.IsNullOrWhiteSpace(audioUrl))
                {
                    await DisplayAlert("Erro",
                        "Não foi possível enviar o áudio.",
                        "OK");
                    return;
                }

                var msg = new ChatMessage
                {
                    ChatId = _chatId,
                    SenderId = CurrentUserId,
                    Text = string.Empty,
                    AudioUrl = audioUrl,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ReadBy = new Dictionary<string, bool>
                    {
                        [CurrentUserId] = true
                    }
                };

                await ChatService.Instance.SendMessageAsync(_chatId, msg);

                AddBubble(msg);
                ScrollToBottom();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatPage] Erro ao anexar áudio: {ex}");
                await DisplayAlert("Erro",
                    "Não foi possível enviar o áudio.",
                    "OK");
            }
        }

        // ===== ENVIO DE MENSAGEM DE TEXTO =====

        private async Task SendCurrentMessageAsync()
        {
            var text = MessageEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (string.IsNullOrWhiteSpace(CurrentUserId))
            {
                await DisplayAlert("Erro",
                    "Usuário atual não identificado. Faça login novamente.",
                    "OK");
                return;
            }

            if (!_isGroupChat && string.IsNullOrWhiteSpace(OtherUserId))
            {
                await DisplayAlert("Erro",
                    "Contato inválido para esta conversa.",
                    "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(_chatId))
            {
                try
                {
                    if (_isGroupChat)
                    {
                        await DisplayAlert("Erro",
                            "Grupo inválido.",
                            "OK");
                        return;
                    }

                    _chatId = await ChatService.Instance.GetOrCreateChatAsync(CurrentUserId, OtherUserId);
                    Console.WriteLine($"[ChatPage] ChatId criado no envio: '{_chatId}'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ChatPage] Erro ao criar chatId no envio: {ex}");
                    await DisplayAlert("Erro",
                        "Não foi possível criar a conversa.",
                        "OK");
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(_chatId))
            {
                await DisplayAlert("Erro",
                    "Id da conversa não definido. Tente sair e entrar na conversa novamente.",
                    "OK");
                return;
            }

            var msg = new ChatMessage
            {
                ChatId = _chatId,
                SenderId = CurrentUserId,
                Text = text,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ReadBy = new Dictionary<string, bool>
                {
                    [CurrentUserId] = true
                }
            };

            try
            {
                await ChatService.Instance.SendMessageAsync(_chatId, msg);

                AddBubble(msg);

                MessageEntry.Text = string.Empty;
                ScrollToBottom();
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"[ChatPage] Erro de argumento ao enviar mensagem: {ex.Message}");

                await DisplayAlert("Erro",
                    "Não foi possível enviar a mensagem. Verifique os dados da conversa.",
                    "OK");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatPage] Erro inesperado ao enviar mensagem: {ex}");
                await DisplayAlert("Erro",
                    "Ocorreu um erro ao enviar a mensagem.",
                    "OK");
            }
        }

        // ===== UI DAS BOLHAS =====

        private void AddBubble(ChatMessage msg)
        {
            bool isMine = msg.SenderId == CurrentUserId;

            var layout = new VerticalStackLayout
            {
                Spacing = 4
            };

            if (!string.IsNullOrWhiteSpace(msg.Text))
            {
                layout.Children.Add(new Label
                {
                    Text = msg.Text,
                    TextColor = isMine ? Colors.White : Color.FromArgb("#262626")
                });
            }

            if (!string.IsNullOrWhiteSpace(msg.ImageBase64))
            {
                try
                {
                    var bytes = Convert.FromBase64String(msg.ImageBase64);
                    var imgSource = ImageSource.FromStream(() => new MemoryStream(bytes));

                    layout.Children.Add(new Image
                    {
                        Source = imgSource,
                        Aspect = Aspect.AspectFill,
                        HeightRequest = 180,
                        WidthRequest = 220
                    });
                }
                catch
                {
                    // se der erro ao decodificar, ignora a imagem
                }
            }

            if (!string.IsNullOrWhiteSpace(msg.AudioUrl))
            {
                var audioButton = new Button
                {
                    Text = "▶ Áudio",
                    FontSize = 12,
                    Padding = new Thickness(8, 4),
                    BackgroundColor = isMine ? Color.FromArgb("#6f3ad4") : Color.FromArgb("#eeeeee"),
                    TextColor = isMine ? Colors.White : Color.FromArgb("#333333"),
                    CornerRadius = 16
                };

                audioButton.Clicked += async (s, e) =>
                {
                    try
                    {
                        await Launcher.OpenAsync(new Uri(msg.AudioUrl));
                    }
                    catch
                    {
                        await DisplayAlert("Erro",
                            "Não foi possível reproduzir o áudio.",
                            "OK");
                    }
                };

                layout.Children.Add(audioButton);
            }

            var bubble = new Frame
            {
                BackgroundColor = isMine ? Color.FromArgb("#5d259c") : Colors.White,
                CornerRadius = 16,
                Padding = new Thickness(10),
                HorizontalOptions = isMine ? LayoutOptions.End : LayoutOptions.Start,
                MaximumWidthRequest = 260,
                HasShadow = false,
                Content = layout
            };

            MessagesStack.Children.Add(bubble);
        }

        private void ScrollToBottom()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await MessagesScroll.ScrollToAsync(0, MessagesStack.Height, true);
                }
                catch
                {
                    // ignora erro de scroll
                }
            });
        }

        // ===== GERENCIAMENTO DE GRUPO (header tap) =====

        private async void OnHeaderTapped(object sender, EventArgs e)
        {
            if (!_isGroupChat || !_isGroupAdmin)
                return;

            var action = await DisplayActionSheet(
                "Gerenciar grupo",
                "Cancelar",
                null,
                "Adicionar membro",
                "Remover membro",
                "Excluir grupo");

            switch (action)
            {
                case "Adicionar membro":
                    await AddMemberFromListAsync();
                    break;

                case "Remover membro":
                    await RemoveMemberFromListAsync();
                    break;

                case "Excluir grupo":
                    await DeleteGroupAsync();
                    break;

                default:
                    break;
            }
        }

        private async Task AddMemberFromListAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_chatId) || string.IsNullOrWhiteSpace(CurrentUserId))
                    return;

                var existingMembers = await ChatService.Instance.GetGroupMemberIdsAsync(_chatId)
                    ?? new List<string>();

                var friends = await FriendService.Instance.GetFriendsAsync(CurrentUserId) ?? new List<string>();
                var matches = await MatchService.Instance.GetMatchesAsync(CurrentUserId) ?? new List<string>();

                var candidateIds = friends
                    .Concat(matches)
                    .Distinct()
                    .Where(id => !string.IsNullOrWhiteSpace(id)
                                 && id != CurrentUserId
                                 && !existingMembers.Contains(id))
                    .ToList();

                if (candidateIds.Count == 0)
                {
                    await DisplayAlert("Aviso",
                        "Não há amigos ou matches disponíveis para adicionar ao grupo.",
                        "OK");
                    return;
                }

                var labelToId = new Dictionary<string, string>();
                var options = new List<string>();

                foreach (var uid in candidateIds)
                {
                    var profile = await FirebaseDatabaseService.Instance.GetUserProfileAsync(uid);
                    var name = profile?.DisplayName ?? "Usuário";
                    var city = profile?.City;
                    var label = string.IsNullOrWhiteSpace(city)
                        ? name
                        : $"{name} ({city})";

                    // Evita duplicar label
                    var finalLabel = label;
                    int dup = 2;
                    while (labelToId.ContainsKey(finalLabel))
                    {
                        finalLabel = $"{label} #{dup}";
                        dup++;
                    }

                    labelToId[finalLabel] = uid;
                    options.Add(finalLabel);
                }

                var chosen = await DisplayActionSheet(
                    "Adicionar ao grupo",
                    "Cancelar",
                    null,
                    options.ToArray());

                if (string.IsNullOrWhiteSpace(chosen) || chosen == "Cancelar")
                    return;

                if (!labelToId.TryGetValue(chosen, out var selectedId))
                    return;

                // 🔥 usa AddGroupMembersAsync com requesterId
                await ChatService.Instance.AddGroupMembersAsync(_chatId, CurrentUserId, new[] { selectedId });

                await DisplayAlert("Pronto",
                    "Membro adicionado ao grupo.",
                    "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro",
                    "Não foi possível adicionar o membro.\n" + ex.Message,
                    "OK");
            }
        }

        private async Task RemoveMemberFromListAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_chatId) || string.IsNullOrWhiteSpace(CurrentUserId))
                    return;

                var memberIds = await ChatService.Instance.GetGroupMemberIdsAsync(_chatId)
                    ?? new List<string>();

                // Não permite remover a si mesmo por aqui
                memberIds = memberIds
                    .Where(id => !string.IsNullOrWhiteSpace(id) && id != CurrentUserId)
                    .ToList();

                if (memberIds.Count == 0)
                {
                    await DisplayAlert("Aviso",
                        "Não há membros para remover (além de você).",
                        "OK");
                    return;
                }

                var labelToId = new Dictionary<string, string>();
                var options = new List<string>();

                foreach (var uid in memberIds)
                {
                    var profile = await FirebaseDatabaseService.Instance.GetUserProfileAsync(uid);
                    var name = profile?.DisplayName ?? "Usuário";
                    var city = profile?.City;
                    var label = string.IsNullOrWhiteSpace(city)
                        ? name
                        : $"{name} ({city})";

                    var finalLabel = label;
                    int dup = 2;
                    while (labelToId.ContainsKey(finalLabel))
                    {
                        finalLabel = $"{label} #{dup}";
                        dup++;
                    }

                    labelToId[finalLabel] = uid;
                    options.Add(finalLabel);
                }

                var chosen = await DisplayActionSheet(
                    "Remover do grupo",
                    "Cancelar",
                    null,
                    options.ToArray());

                if (string.IsNullOrWhiteSpace(chosen) || chosen == "Cancelar")
                    return;

                if (!labelToId.TryGetValue(chosen, out var selectedId))
                    return;

                // 🔥 usa RemoveGroupMemberAsync com requesterId
                await ChatService.Instance.RemoveGroupMemberAsync(_chatId, CurrentUserId, selectedId);

                await DisplayAlert("Pronto",
                    "Membro removido do grupo.",
                    "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro",
                    "Não foi possível remover o membro.\n" + ex.Message,
                    "OK");
            }
        }

        private async Task DeleteGroupAsync()
        {
            var confirm = await DisplayAlert(
                "Excluir grupo",
                "Tem certeza que deseja excluir este grupo para todos os membros?",
                "Excluir",
                "Cancelar");

            if (!confirm)
                return;

            try
            {
                if (string.IsNullOrWhiteSpace(_chatId) || string.IsNullOrWhiteSpace(CurrentUserId))
                    return;

                // 🔥 agora passa também o requesterId
                await ChatService.Instance.DeleteGroupAsync(_chatId, CurrentUserId);

                await DisplayAlert("Pronto",
                    "Grupo excluído.",
                    "OK");

                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro",
                    "Não foi possível excluir o grupo.\n" + ex.Message,
                    "OK");
            }
        }

        // ===== CHAMADA EM TEMPO REAL (Jitsi) =====

        private async void OnCallTapped(object sender, EventArgs e)
        {
            try
            {
                if (_isGroupChat)
                {
                    await DisplayAlert("Aviso",
                        "Chamada em grupo ainda não foi implementada.",
                        "OK");
                    return;
                }

                if (string.IsNullOrWhiteSpace(CurrentUserId) || string.IsNullOrWhiteSpace(OtherUserId))
                {
                    await DisplayAlert("Erro",
                        "Usuário ou contato inválido para iniciar chamada.",
                        "OK");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_chatId))
                {
                    _chatId = await ChatService.Instance.GetOrCreateChatAsync(CurrentUserId, OtherUserId);
                }

                var roomName = $"amoraapp-{_chatId}";
                var callUrl = $"https://meet.jit.si/{Uri.EscapeDataString(roomName)}";

                await Launcher.OpenAsync(callUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatPage] Erro ao iniciar chamada: {ex}");
                await DisplayAlert("Erro",
                    "Não foi possível iniciar a chamada.",
                    "OK");
            }
        }
    }
}
