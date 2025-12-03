using AmoraApp.Models;
using AmoraApp.Services;
using AmoraApp.ViewModels;
using Microsoft.Maui.Controls;
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

        /// <summary>
        /// Tap em avatar ou item da lista de chats.
        /// </summary>
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

            var options = new List<string>
            {
                "Abrir conversa"
            };

            if (chat.IsGroup)
            {
                // Todos podem ver a lista de membros
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

                var favLabel = chat.IsFavorite
                    ? "Remover dos favoritos"
                    : "Adicionar aos favoritos";
                options.Add(favLabel);
            }
            else
            {
                // NÃO é grupo => contato normal
                if (!string.IsNullOrWhiteSpace(chat.UserId))
                {
                    options.Add("Criar grupo com este contato");
                }

                var favLabel = chat.IsFavorite
                    ? "Remover dos favoritos"
                    : "Adicionar aos favoritos";
                options.Add(favLabel);

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
                    if (Vm.ToggleFavoriteCommand.CanExecute(chat))
                        Vm.ToggleFavoriteCommand.Execute(chat);
                    break;

                case "Bloquear usuário":
                    if (Vm.BlockChatCommand.CanExecute(chat))
                        Vm.BlockChatCommand.Execute(chat);
                    break;

                case "Excluir chat":
                    if (Vm.DeleteChatCommand.CanExecute(chat))
                        Vm.DeleteChatCommand.Execute(chat);
                    break;

                // ===== GRUPO =====
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

                default:
                    break;
            }
        }

        private async Task CreateGroupWithContactAsync(ChatItem chat)
        {
            try
            {
                var me = FirebaseAuthService.Instance.CurrentUserUid;
                if (string.IsNullOrWhiteSpace(me))
                {
                    await DisplayAlert("Erro",
                        "Usuário não logado.",
                        "OK");
                    return;
                }

                if (string.IsNullOrWhiteSpace(chat.UserId))
                {
                    await DisplayAlert("Erro",
                        "Contato inválido para criar grupo.",
                        "OK");
                    return;
                }

                var groupName = await DisplayPromptAsync(
                    "Novo grupo",
                    "Nome do grupo:",
                    "Criar",
                    "Cancelar");

                if (string.IsNullOrWhiteSpace(groupName))
                    return;

                var trimmedName = groupName.Trim();

                var chatIdGroup = await ChatService.Instance.CreateGroupChatAsync(
                    me,
                    trimmedName,
                    new[] { me, chat.UserId });

                var groupItem = new ChatItem
                {
                    ChatId = chatIdGroup,
                    IsGroup = true,
                    GroupName = trimmedName,
                    DisplayName = $"Grupo {trimmedName}",
                    MembersCount = 2,
                    IsGroupAdmin = true
                };

                await Navigation.PushAsync(new ChatPage(groupItem));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro",
                    "Não foi possível criar o grupo.\n" + ex.Message,
                    "OK");
            }
        }

        // ================== VER MEMBROS DO GRUPO ==================

        private async Task ShowGroupMembersAsync(ChatItem chat)
        {
            try
            {
                if (chat == null || string.IsNullOrWhiteSpace(chat.ChatId))
                    return;

                var memberIds = await ChatService.Instance.GetGroupMemberIdsAsync(chat.ChatId);
                if (memberIds == null || memberIds.Count == 0)
                {
                    await DisplayAlert("Membros", "Nenhum membro encontrado neste grupo.", "OK");
                    return;
                }

                var profiles = new List<UserProfile>();
                foreach (var id in memberIds)
                {
                    var p = await FirebaseDatabaseService.Instance.GetUserProfileAsync(id);
                    if (p != null)
                        profiles.Add(p);
                }

                if (profiles.Count == 0)
                {
                    await DisplayAlert("Membros", "Não foi possível carregar os perfis dos membros.", "OK");
                    return;
                }

                var options = profiles
                    .Select(p => string.IsNullOrWhiteSpace(p.DisplayName) ? p.Id : p.DisplayName)
                    .ToArray();

                var chosen = await DisplayActionSheet(
                    "Membros do grupo",
                    "Cancelar",
                    null,
                    options);

                if (string.IsNullOrWhiteSpace(chosen) || chosen == "Cancelar")
                    return;

                var selectedProfile = profiles.FirstOrDefault(p =>
                    (string.IsNullOrWhiteSpace(p.DisplayName) ? p.Id : p.DisplayName) == chosen);

                if (selectedProfile == null)
                    return;

                // Abre Discover focado nesse membro
                await Navigation.PushAsync(new DiscoverPage(selectedProfile));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro",
                    "Não foi possível carregar os membros do grupo.\n" + ex.Message,
                    "OK");
            }
        }

        // ================== GERENCIAMENTO DE GRUPO ==================

        private async Task LeaveGroupAsync(ChatItem chat)
        {
            try
            {
                var me = FirebaseAuthService.Instance.CurrentUserUid;
                if (string.IsNullOrWhiteSpace(me))
                    return;

                await ChatService.Instance.LeaveGroupAsync(chat.ChatId, me);

                if (Vm != null)
                    await Vm.InitializeAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro",
                    "Não foi possível sair do grupo.\n" + ex.Message,
                    "OK");
            }
        }

        private async Task AddMembersToGroupAsync(ChatItem chat)
        {
            try
            {
                var me = FirebaseAuthService.Instance.CurrentUserUid;
                if (string.IsNullOrWhiteSpace(me))
                    return;

                if (!chat.IsGroupAdmin)
                {
                    await DisplayAlert("Erro",
                        "Apenas o administrador do grupo pode adicionar membros.",
                        "OK");
                    return;
                }

                // Carrega todos os usuários possíveis a partir do seu serviço
                // Aqui vou usar MatchService para simplicidade: você pode ajustar se quiser restringir.
                var allUsers = await MatchService.Instance.GetUsersForDiscoverAsync(me);

                // Remove quem já está no grupo
                var memberIds = await ChatService.Instance.GetGroupMemberIdsAsync(chat.ChatId);
                var candidates = allUsers
                    .Where(u => !memberIds.Contains(u.Id))
                    .ToList();

                if (candidates.Count == 0)
                {
                    await DisplayAlert("Adicionar membros",
                        "Nenhum usuário disponível para adicionar.",
                        "OK");
                    return;
                }

                // Monta lista de nomes para escolher (um por vez, mais simples)
                var options = candidates
                    .Select(u => string.IsNullOrWhiteSpace(u.DisplayName) ? u.Id : u.DisplayName)
                    .ToArray();

                var chosen = await DisplayActionSheet(
                    "Adicionar membro",
                    "Cancelar",
                    null,
                    options);

                if (string.IsNullOrWhiteSpace(chosen) || chosen == "Cancelar")
                    return;

                var selected = candidates.FirstOrDefault(u =>
                    (string.IsNullOrWhiteSpace(u.DisplayName) ? u.Id : u.DisplayName) == chosen);

                if (selected == null)
                    return;

                await ChatService.Instance.AddGroupMembersAsync(chat.ChatId, me, new[] { selected.Id });

                if (Vm != null)
                    await Vm.InitializeAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro",
                    "Não foi possível adicionar o membro.\n" + ex.Message,
                    "OK");
            }
        }

        private async Task RemoveMembersFromGroupAsync(ChatItem chat)
        {
            try
            {
                var me = FirebaseAuthService.Instance.CurrentUserUid;
                if (string.IsNullOrWhiteSpace(me))
                    return;

                if (!chat.IsGroupAdmin)
                {
                    await DisplayAlert("Erro",
                        "Apenas o administrador do grupo pode remover membros.",
                        "OK");
                    return;
                }

                var memberIds = await ChatService.Instance.GetGroupMemberIdsAsync(chat.ChatId);
                if (memberIds == null || memberIds.Count <= 1)
                {
                    await DisplayAlert("Remover membros",
                        "Não há membros suficientes para remover.",
                        "OK");
                    return;
                }

                var profiles = new List<UserProfile>();
                foreach (var id in memberIds)
                {
                    var p = await FirebaseDatabaseService.Instance.GetUserProfileAsync(id);
                    if (p != null)
                        profiles.Add(p);
                }

                // Não permite remover a si mesmo aqui (use "Sair do grupo" pra isso)
                profiles = profiles.Where(p => p.Id != me).ToList();

                if (profiles.Count == 0)
                {
                    await DisplayAlert("Remover membros",
                        "Nenhum membro disponível para remoção.",
                        "OK");
                    return;
                }

                var options = profiles
                    .Select(p => string.IsNullOrWhiteSpace(p.DisplayName) ? p.Id : p.DisplayName)
                    .ToArray();

                var chosen = await DisplayActionSheet(
                    "Remover membro",
                    "Cancelar",
                    null,
                    options);

                if (string.IsNullOrWhiteSpace(chosen) || chosen == "Cancelar")
                    return;

                var selected = profiles.FirstOrDefault(p =>
                    (string.IsNullOrWhiteSpace(p.DisplayName) ? p.Id : p.DisplayName) == chosen);

                if (selected == null)
                    return;

                await ChatService.Instance.RemoveGroupMemberAsync(chat.ChatId, me, selected.Id);

                if (Vm != null)
                    await Vm.InitializeAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro",
                    "Não foi possível remover o membro.\n" + ex.Message,
                    "OK");
            }
        }

        private async Task DeleteGroupAsync(ChatItem chat)
        {
            try
            {
                var me = FirebaseAuthService.Instance.CurrentUserUid;
                if (string.IsNullOrWhiteSpace(me))
                    return;

                if (!chat.IsGroupAdmin)
                {
                    await DisplayAlert("Erro",
                        "Apenas o administrador do grupo pode excluir o grupo.",
                        "OK");
                    return;
                }

                var confirm = await DisplayAlert(
                    "Excluir grupo",
                    "Tem certeza que deseja excluir este grupo para todos os participantes?",
                    "Sim",
                    "Não");

                if (!confirm)
                    return;

                await ChatService.Instance.DeleteGroupAsync(chat.ChatId, me);

                if (Vm != null)
                    await Vm.InitializeAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro",
                    "Não foi possível excluir o grupo.\n" + ex.Message,
                    "OK");
            }
        }
    }
}
