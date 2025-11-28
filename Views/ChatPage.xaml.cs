using AmoraApp.Models;
using AmoraApp.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AmoraApp.Views
{
    public partial class ChatPage : ContentPage
    {
        public string OtherUserId { get; private set; } = string.Empty;
        public string OtherUserName { get; private set; } = "Contato";

        private string _chatId = string.Empty;

        // Agora pega o UID diretamente do FirebaseAuthService
        private string CurrentUserId =>
            FirebaseAuthService.Instance.CurrentUserUid ?? string.Empty;

        public ChatPage(string otherUserId, string otherUserName)
        {
            InitializeComponent();

            OtherUserId = otherUserId ?? string.Empty;
            OtherUserName = string.IsNullOrWhiteSpace(otherUserName) ? "Contato" : otherUserName;

            UserNameLabel.Text = OtherUserName;

            Console.WriteLine($"[ChatPage] Novo chat com usuário '{OtherUserId}' ({OtherUserName})");
        }

        // Construtor sem parâmetros – mantém por compatibilidade
        public ChatPage()
        {
            InitializeComponent();
            OtherUserId = string.Empty;
            OtherUserName = "Contato";

            UserNameLabel.Text = OtherUserName;

            Console.WriteLine("[ChatPage] ATENÇÃO: construtor sem parâmetros foi usado. Passe sempre (userId, nome) ao navegar.");
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (string.IsNullOrWhiteSpace(CurrentUserId) || string.IsNullOrWhiteSpace(OtherUserId))
            {
                Console.WriteLine($"[ChatPage] IDs inválidos ao aparecer. CurrentUserId='{CurrentUserId}', OtherUserId='{OtherUserId}'");
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
        }

        // ===== PRESENÇA DO OUTRO USUÁRIO =====

        private async Task UpdateOtherUserStatusAsync()
        {
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

        // Novo: clique no botão de imagem
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

                if (string.IsNullOrWhiteSpace(OtherUserId))
                {
                    await DisplayAlert("Erro",
                        "Contato inválido para esta conversa.",
                        "OK");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_chatId))
                {
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
                    Text = string.Empty,          // preview é tratado no ChatService
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

            if (string.IsNullOrWhiteSpace(OtherUserId))
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

        // ===== CHAMADA EM TEMPO REAL (Jitsi) =====

        private async void OnCallTapped(object sender, EventArgs e)
        {
            try
            {
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

                // Usa Jitsi Meet (gratuito) com uma sala baseada no chatId
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
