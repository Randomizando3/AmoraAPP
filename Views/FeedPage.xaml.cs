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

        private async void OnRequestsTapped(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new FriendRequestsPage());
        }

        private async void OnFriendsClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new FriendsPage());
        }

        private async void OnStoryTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not StoryBubble bubble)
                return;

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

                using var stream = await GetImageStreamForStoryAsync(action);
                if (stream == null)
                    return;

                var fileName = $"stories/{uid}/{Guid.NewGuid():N}.jpg";
                var url = await FirebaseStorageService.Instance.UploadImageAsync(stream, fileName);
                if (string.IsNullOrEmpty(url))
                    return;

                await StoryService.Instance.AddStoryAsync(uid, url);

                if (_vm != null)
                    await _vm.LoadFeedCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", ex.Message, "OK");
            }
        }

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
                    vm.NewPostImageUrl = url;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", ex.Message, "OK");
            }
        }

        private async void OnCommentClicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.CommandParameter is not Post post)
                return;

            await Navigation.PushModalAsync(new CommentsPage(post));
        }

        // ===== IMAGEM EM TELA CHEIA =====

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

        // ==========================================================
        //  MENU DE OPÇÕES DO POST (⋯) — DENUNCIAR
        // ==========================================================
        private async void OnPostOptionsClicked(object sender, EventArgs e)
        {
            try
            {
                if ((sender as Button)?.CommandParameter is not Post post)
                    return;

                var me = FirebaseAuthService.Instance.CurrentUserUid;
                if (string.IsNullOrWhiteSpace(me))
                {
                    await DisplayAlert("Erro", "Usuário não logado.", "OK");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(post.UserId) && post.UserId == me)
                {
                    await DisplayAlert("Aviso", "Você não pode denunciar seu próprio conteúdo.", "OK");
                    return;
                }

                var action = await DisplayActionSheet(
                    "Opções",
                    "Cancelar",
                    null,
                    "Denunciar publicação",
                    "Denunciar perfil");

                if (string.IsNullOrWhiteSpace(action) || action == "Cancelar")
                    return;

                var reason = await AskReportReasonAsync();
                if (string.IsNullOrWhiteSpace(reason))
                    return;

                var extra = await DisplayPromptAsync(
                    "Detalhes (opcional)",
                    "Se quiser, descreva rapidamente o motivo:",
                    "Enviar",
                    "Pular",
                    maxLength: 180,
                    keyboard: Keyboard.Text);

                var report = new ReportItem
                {
                    ReporterUserId = me,
                    TargetUserId = post.UserId ?? string.Empty,
                    TargetUserName = post.UserName ?? string.Empty,
                    CreatedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    AppArea = "feed",
                    Reason = reason,
                    ExtraDetails = extra ?? string.Empty
                };

                if (action == "Denunciar publicação")
                {
                    report.Type = "post";
                    report.PostId = post.Id ?? string.Empty;

                    var preview = (post.Text ?? string.Empty).Trim();
                    if (preview.Length > 140)
                        preview = preview.Substring(0, 140);

                    report.PostTextPreview = preview;
                    report.PostImageUrl = post.ImageUrl ?? string.Empty;
                }
                else
                {
                    report.Type = "profile";
                }

                var reportId = await FirebaseDatabaseService.Instance.CreateReportAsync(report);

                if (string.IsNullOrWhiteSpace(reportId))
                {
                    await DisplayAlert("Aviso", "Sua denúncia não gerou ID no servidor. Verifique regras do Firebase.", "OK");
                    return;
                }

                await DisplayAlert("Denúncia enviada", "Obrigado. Vamos analisar o conteúdo.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", "Não foi possível enviar a denúncia:\n" + ex.Message, "OK");
            }
        }

        private async System.Threading.Tasks.Task<string?> AskReportReasonAsync()
        {
            var reason = await DisplayActionSheet(
                "Qual o motivo?",
                "Cancelar",
                null,
                "Spam",
                "Nudez / conteúdo sexual",
                "Ódio / discurso de ódio",
                "Violência",
                "Assédio",
                "Golpe / fraude",
                "Outros");

            if (string.IsNullOrWhiteSpace(reason) || reason == "Cancelar")
                return null;

            return reason;
        }
    }
}
