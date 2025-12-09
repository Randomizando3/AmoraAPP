using AmoraApp.Helpers;
using AmoraApp.Models;
using AmoraApp.Services;
using AmoraApp.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AmoraApp.Views
{
    public partial class ChatListPage : ContentPage
    {
        private MessagesViewModel Vm => BindingContext as MessagesViewModel;

        public ChatListPage()
        {
            InitializeComponent();
            BindingContext = new MessagesViewModel();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (Vm != null)
                await Vm.InitializeAsync();
        }

        // ============================================================
        //  TAP EM UM CHAT
        // ============================================================
        private async void OnChatTapped(object sender, EventArgs e)
        {
            if (Vm == null)
                return;

            if (e is not TappedEventArgs tapped ||
                tapped.Parameter is not ChatItem chat)
                return;

            var uid = FirebaseAuthService.Instance.CurrentUserUid;
            if (string.IsNullOrWhiteSpace(uid))
            {
                await DisplayAlert("Erro", "Usuário não logado.", "OK");
                return;
            }

            var options = new List<string> { "Abrir conversa" };

            if (chat.IsGroup)
            {
                options.Add("Ver membros do grupo");

                if (chat.IsGroupAdmin)
                {
                    options.Add("Adicionar membros");
                    options.Add("Remover membros");
                    options.Add("Excluir grupo");
                }
                else
                {
                    options.Add("Sair do grupo");
                }

                options.Add(chat.IsFavorite ? "Remover dos favoritos" : "Adicionar aos favoritos");
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(chat.UserId))
                    options.Add("Criar grupo com este contato");

                options.Add(chat.IsFavorite ? "Remover dos favoritos" : "Adicionar aos favoritos");

                options.Add("Bloquear usuário");
                options.Add("Excluir chat");
            }

            var action = await DisplayActionSheet(
                chat.DisplayName,
                "Cancelar",
                null,
                options.ToArray());

            switch (action)
            {
                case "Abrir conversa":
                    await Navigation.PushAsync(new ChatPage(chat));
                    break;

                case "Criar grupo com este contato":
                    await CreateGroupWithContactAsync(chat);
                    break;

                case "Adicionar aos favoritos":
                case "Remover dos favoritos":
                    Vm.ToggleFavoriteCommand.Execute(chat);
                    break;

                case "Bloquear usuário":
                    Vm.BlockChatCommand.Execute(chat);
                    break;

                case "Excluir chat":
                    Vm.DeleteChatCommand.Execute(chat);
                    break;

                case "Ver membros do grupo":
                    await ShowGroupMembersAsync(chat);
                    break;

                case "Sair do grupo":
                    await LeaveGroupAsync(chat);
                    break;

                case "Adicionar membros":
                    await AddMembersToGroupAsync(chat);
                    break;

                case "Remover membros":
                    await RemoveMembersFromGroupAsync(chat);
                    break;

                case "Excluir grupo":
                    await DeleteGroupAsync(chat);
                    break;
            }
        }

        // ============================================================
        //  CRIAR NOVO GRUPO COM UM CONTATO
        // ============================================================
        private async Task CreateGroupWithContactAsync(ChatItem chat)
        {
            try
            {
                var me = FirebaseAuthService.Instance.CurrentUserUid;
                if (string.IsNullOrWhiteSpace(me))
                {
                    await DisplayAlert("Erro", "Usuário não logado.", "OK");
                    return;
                }

                if (string.IsNullOrWhiteSpace(chat.UserId))
                {
                    await DisplayAlert("Erro", "Contato inválido para criar grupo.", "OK");
                    return;
                }

                // 1) Nome do grupo
                var groupName = await DisplayPromptAsync(
                    "Novo grupo",
                    "Nome do grupo:",
                    "OK",
                    "Cancelar");

                if (string.IsNullOrWhiteSpace(groupName))
                    return;

                var trimmed = groupName.Trim();

                // 2) Pergunta sobre foto
                var photoOption = await DisplayActionSheet(
                    "Avatar do grupo",
                    "Cancelar",
                    null,
                    "Escolher foto",
                    "Usar avatar automático");

                string? finalPhotoUrl = null;

                // 3) Foto selecionada pelo usuário
                if (photoOption == "Escolher foto")
                {
                    var pick = await FilePicker.PickAsync(new PickOptions
                    {
                        FileTypes = FilePickerFileType.Images,
                        PickerTitle = "Escolher foto"
                    });

                    if (pick != null)
                    {
                        await using var s = await pick.OpenReadAsync();
                        finalPhotoUrl = await FirebaseStorageService.Instance
                            .UploadImageAsync(s, $"groupAvatars/{Guid.NewGuid():N}.jpg");
                    }
                }

                // 4) Criar grupo
                var chatId = await ChatService.Instance.CreateGroupChatAsync(
                    me, trimmed, new[] { me, chat.UserId });

                // 5) Se não houver foto manual, gerar automático (emoji)
                if (string.IsNullOrWhiteSpace(finalPhotoUrl))
                {
                    string[] emojis = { "🍇", "🐱", "🍀", "🦊", "🌈", "🔥", "🎀", "⭐" };
                    var emoji = emojis[new Random(trimmed.GetHashCode()).Next(emojis.Length)];

                    // Criamos um unique URL lógico somente para usar a função que detecta avatar automático
                    finalPhotoUrl = $"autoAvatar://{emoji}/#FFFFFF";
                }

                // 6) Salvar no grupo
                await FirebaseDatabaseService.Instance.UpdateChatPhotoAsync(chatId, finalPhotoUrl);

                // 7) Abrir
                await Navigation.PushAsync(new ChatPage(new ChatItem
                {
                    ChatId = chatId,
                    IsGroup = true,
                    GroupName = trimmed,
                    DisplayName = $"Grupo {trimmed}",
                    PhotoUrl = finalPhotoUrl,
                    IsGroupAdmin = true,
                    MembersCount = 2
                }));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", "Falha ao criar grupo.\n" + ex.Message, "OK");
            }
        }

        // ============================================================
        //  VER MEMBROS DO GRUPO
        // ============================================================
        private async Task ShowGroupMembersAsync(ChatItem chat)
        {
            try
            {
                var ids = await ChatService.Instance.GetGroupMemberIdsAsync(chat.ChatId);
                if (ids == null || ids.Count == 0)
                {
                    await DisplayAlert("Membros", "Nenhum membro encontrado.", "OK");
                    return;
                }

                var list = new List<UserProfile>();
                foreach (var id in ids)
                {
                    var p = await FirebaseDatabaseService.Instance.GetUserProfileAsync(id);
                    if (p != null) list.Add(p);
                }

                var names = list.Select(p => p.DisplayName ?? p.Id).ToArray();

                var chosen = await DisplayActionSheet("Membros", "Cancelar", null, names);
                if (string.IsNullOrWhiteSpace(chosen) || chosen == "Cancelar")
                    return;

                var prof = list.FirstOrDefault(p => p.DisplayName == chosen || p.Id == chosen);
                if (prof != null)
                    await Navigation.PushAsync(new DiscoverPage(prof));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", "Falha ao carregar membros.\n" + ex.Message, "OK");
            }
        }

        // ============================================================
        //  SAIR DO GRUPO
        // ============================================================
        private async Task LeaveGroupAsync(ChatItem chat)
        {
            try
            {
                var me = FirebaseAuthService.Instance.CurrentUserUid;
                await ChatService.Instance.LeaveGroupAsync(chat.ChatId, me);

                if (Vm != null)
                    await Vm.InitializeAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", ex.Message, "OK");
            }
        }

        // ============================================================
        //  ADICIONAR MEMBROS
        // ============================================================
        private async Task AddMembersToGroupAsync(ChatItem chat)
        {
            try
            {
                if (!chat.IsGroupAdmin)
                {
                    await DisplayAlert("Erro", "Somente admins podem adicionar.", "OK");
                    return;
                }

                var me = FirebaseAuthService.Instance.CurrentUserUid;
                var users = await MatchService.Instance.GetUsersForDiscoverAsync(me);

                var members = await ChatService.Instance.GetGroupMemberIdsAsync(chat.ChatId);

                var list = users.Where(u => !members.Contains(u.Id)).ToList();

                if (list.Count == 0)
                {
                    await DisplayAlert("Aviso", "Nenhum usuário disponível.", "OK");
                    return;
                }

                var names = list.Select(u => u.DisplayName ?? u.Id).ToArray();

                var chosen = await DisplayActionSheet("Adicionar membro", "Cancelar", null, names);
                if (string.IsNullOrWhiteSpace(chosen) || chosen == "Cancelar")
                    return;

                var pick = list.First(u => u.DisplayName == chosen || u.Id == chosen);

                await ChatService.Instance.AddGroupMembersAsync(chat.ChatId, me, new[] { pick.Id });

                await Vm.InitializeAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", ex.Message, "OK");
            }
        }

        // ============================================================
        //  REMOVER MEMBROS
        // ============================================================
        private async Task RemoveMembersFromGroupAsync(ChatItem chat)
        {
            try
            {
                if (!chat.IsGroupAdmin)
                {
                    await DisplayAlert("Erro", "Somente admins podem remover.", "OK");
                    return;
                }

                var me = FirebaseAuthService.Instance.CurrentUserUid;
                var ids = await ChatService.Instance.GetGroupMemberIdsAsync(chat.ChatId);

                var profiles = new List<UserProfile>();
                foreach (var id in ids)
                {
                    var p = await FirebaseDatabaseService.Instance.GetUserProfileAsync(id);
                    if (p != null && p.Id != me)
                        profiles.Add(p);
                }

                if (profiles.Count == 0)
                {
                    await DisplayAlert("Aviso", "Nenhum membro pode ser removido.", "OK");
                    return;
                }

                var names = profiles.Select(p => p.DisplayName ?? p.Id).ToArray();

                var chosen = await DisplayActionSheet("Remover membro", "Cancelar", null, names);
                if (string.IsNullOrWhiteSpace(chosen) || chosen == "Cancelar")
                    return;

                var selected = profiles.First(p => p.DisplayName == chosen || p.Id == chosen);

                await ChatService.Instance.RemoveGroupMemberAsync(chat.ChatId, me, selected.Id);

                await Vm.InitializeAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", ex.Message, "OK");
            }
        }

        // ============================================================
        //  EXCLUIR GRUPO
        // ============================================================
        private async Task DeleteGroupAsync(ChatItem chat)
        {
            try
            {
                if (!chat.IsGroupAdmin)
                {
                    await DisplayAlert("Erro", "Somente admins podem excluir.", "OK");
                    return;
                }

                var confirm = await DisplayAlert(
                    "Excluir grupo",
                    "Deseja excluir este grupo para todos?",
                    "Sim", "Não");

                if (!confirm)
                    return;

                var me = FirebaseAuthService.Instance.CurrentUserUid;

                await ChatService.Instance.DeleteGroupAsync(chat.ChatId, me);

                await Vm.InitializeAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", ex.Message, "OK");
            }
        }
    }
}
