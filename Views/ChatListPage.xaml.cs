using AmoraApp.Models;
using AmoraApp.ViewModels;
using Microsoft.Maui.Controls;
using System;

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

            string favLabel = chat.IsFavorite
                ? "Remover dos favoritos"
                : "Adicionar aos favoritos";

            var action = await DisplayActionSheet(
                chat.DisplayName,
                "Cancelar",
                null,
                "Abrir conversa",
                favLabel,
                "Bloquear usuário",
                "Excluir chat");

            switch (action)
            {
                case "Abrir conversa":
                    if (!string.IsNullOrWhiteSpace(chat.UserId))
                    {
                        // IMPORTANTE: sempre abrimos o ChatPage com o UserId e o nome do outro usuário
                        await Navigation.PushAsync(new ChatPage(chat.UserId, chat.DisplayName));
                    }
                    else
                    {
                        await DisplayAlert("Erro",
                            "Usuário inválido para iniciar a conversa.",
                            "OK");
                    }
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
    }
}
