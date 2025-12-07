using System;
using System.Linq;
using AmoraApp.Models;
using AmoraApp.Services;
using AmoraApp.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Media;

namespace AmoraApp.Views
{
    public partial class FeedPage : ContentPage
    {
        private readonly FeedViewModel _vm;

        public FeedPage(FeedViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            BindingContext = _vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (_vm != null)
                await _vm.LoadFeedCommand.ExecuteAsync(null);
        }

        // Abrir solicitações de amizade (ícone 👥)
        private async void OnRequestsTapped(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new FriendRequestsPage());
        }

        // Abrir lista de amigos
        private async void OnFriendsClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new FriendsPage());
        }

        // Tap no story → abre viewer (se tiver stories)
        private async void OnStoryTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not StoryBubble bubble)
                return;

            // Se não tem story, não faz nada
            if (!bubble.HasStory)
                return;

            if (BindingContext is not FeedViewModel vm)
                return;

            var bubbles = vm.Stories;
            if (bubbles == null || bubbles.Count == 0)
                return;

            var index = bubbles.IndexOf(bubble);
            if (index < 0)
                index = 0;

            await Navigation.PushModalAsync(new StoryViewerPage(bubbles.ToList(), index));
        }

        // "+" no story do próprio usuário → cria novo story (Galeria ou Câmera)
        private async void OnAddStoryTapped(object sender, TappedEventArgs e)
        {
            try
            {
                var uid = FirebaseAuthService.Instance.CurrentUserUid;
                if (string.IsNullOrEmpty(uid))
                    return;

                var action = await DisplayActionSheet(
                    "Adicionar story",
                    "Cancelar",
                    null,
                    "Galeria",
                    "Câmera");

                if (string.IsNullOrEmpty(action) || action == "Cancelar")
                    return;

                // Stream da imagem que será enviada
                using var stream = await GetImageStreamForStoryAsync(action);
                if (stream == null)
                    return;

                var fileName = $"stories/{uid}/{Guid.NewGuid():N}.jpg";
                var url = await FirebaseStorageService.Instance.UploadImageAsync(stream, fileName);
                if (string.IsNullOrEmpty(url))
                    return;

                await StoryService.Instance.AddStoryAsync(uid, url);

                // Recarrega feed para atualizar bolhas
                if (_vm != null)
                    await _vm.LoadFeedCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", ex.Message, "OK");
            }
        }

        // Helper para pegar a imagem do story (Galeria ou Câmera)
        private async System.Threading.Tasks.Task<System.IO.Stream?> GetImageStreamForStoryAsync(string action)
        {
            if (action == "Galeria")
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    FileTypes = FilePickerFileType.Images,
                    PickerTitle = "Selecione uma imagem para seu story"
                });

                if (result == null)
                    return null;

                return await result.OpenReadAsync();
            }
            else if (action == "Câmera")
            {
                if (!MediaPicker.Default.IsCaptureSupported)
                {
                    await DisplayAlert("Câmera não disponível", "Este dispositivo não suporta captura de fotos.", "OK");
                    return null;
                }

                var photo = await MediaPicker.Default.CapturePhotoAsync();
                if (photo == null)
                    return null;

                return await photo.OpenReadAsync();
            }

            return null;
        }

        // Botão da câmera no "novo post" (só galeria por enquanto)
        private async void OnAddImageClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    FileTypes = FilePickerFileType.Images,
                    PickerTitle = "Selecione uma imagem"
                });

                if (result == null)
                    return;

                await using var stream = await result.OpenReadAsync();

                var uid = FirebaseAuthService.Instance.CurrentUserUid;
                if (string.IsNullOrEmpty(uid))
                    return;

                var fileName = $"posts/{uid}/{Guid.NewGuid():N}.jpg";
                var url = await FirebaseStorageService.Instance.UploadImageAsync(stream, fileName);

                if (!string.IsNullOrEmpty(url) && BindingContext is FeedViewModel vm)
                {
                    // Ao setar aqui, o preview aparece no card de novo post
                    vm.NewPostImageUrl = url;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", ex.Message, "OK");
            }
        }

        // Abrir tela de comentários
        private async void OnCommentClicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.CommandParameter is not Post post)
                return;

            await Navigation.PushModalAsync(new CommentsPage(post));
        }

        // ===== IMAGEM DO POST EM TELA CHEIA (overlay) =====

        private void OnPostImageTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not Post post)
                return;

            if (string.IsNullOrWhiteSpace(post.ImageUrl))
                return;

            FullImageView.Source = post.ImageUrl;
            ImageOverlay.IsVisible = true;
        }

        private void OnCloseFullImageClicked(object sender, EventArgs e)
        {
            ImageOverlay.IsVisible = false;
            FullImageView.Source = null;
        }
    }
}
