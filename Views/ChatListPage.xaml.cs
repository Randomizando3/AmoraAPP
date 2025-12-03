using AmoraApp.Models;
using AmoraApp.Services;
using AmoraApp.ViewModels;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
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

            var favLabel = chat.IsFavorite
                ? "Remover dos favoritos"
                : "Adicionar aos favoritos";

            if (chat.IsGroup)
            {
                // Grupo: por aqui só abre conversa e favorita.
                options.Add(favLabel);
            }
            else
            {
                // Chat 1x1: opções extras
                if (!string.IsNullOrWhiteSpace(chat.UserId))
                {
                    options.Add("Criar grupo com este contato");
                }

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
                    // Abre a página de chat passando o ChatItem completo (suporta 1x1 e grupos)
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

                default:
                    break;
            }
        }

        /// <summary>
        /// Cria rapidamente um grupo com o contato selecionado (você + ele).
        /// Depois abre diretamente a ChatPage do grupo.
        /// </summary>
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
    }
}
