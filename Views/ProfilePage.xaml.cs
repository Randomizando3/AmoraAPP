using System;
using System.Linq;
using System.Threading.Tasks;
using AmoraApp.Services;
using AmoraApp.ViewModels;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;

namespace AmoraApp.Views
{
    public partial class ProfilePage : ContentPage
    {
        private readonly ProfileViewModel _vm;

        public ProfilePage() : this(new ProfileViewModel())
        {
        }

        public ProfilePage(ProfileViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            BindingContext = _vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            await _vm.LoadAsync();
            await _vm.UpdateLocationAsync();
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            if (Navigation.NavigationStack.Count > 1)
                await Navigation.PopAsync();
        }

        private void OnRootTapped(object sender, EventArgs e)
        {
            if (CityEntry.IsFocused)
                CityEntry.Unfocus();

            if (JobEntry.IsFocused)
                JobEntry.Unfocus();

            _vm.JobSuggestions.Clear();
            _vm.IsJobSuggestionsVisible = false;
        }

        private void JobEntry_Focused(object sender, FocusEventArgs e)
        {
            _vm.OnJobTextChanged(JobEntry.Text ?? string.Empty);
        }

        private void JobEntry_Unfocused(object sender, FocusEventArgs e)
        {
            _vm.JobSuggestions.Clear();
            _vm.IsJobSuggestionsVisible = false;
        }

        private void JobEntry_TextChanged(object sender, TextChangedEventArgs e)
        {
            _vm.OnJobTextChanged(e.NewTextValue ?? string.Empty);
        }

        private void OnJobSuggestionSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection?.FirstOrDefault() is string job)
            {
                _vm.JobTitle = job;
                _vm.JobSuggestions.Clear();
                _vm.IsJobSuggestionsVisible = false;

                if (sender is CollectionView cv)
                    cv.SelectedItem = null;
            }
        }

        // ===== NOVO: handler do botão Admin =====
        private async void OnAdminClicked(object sender, EventArgs e)
        {
            // Proteção adicional (o botão já some pelo IsVisible, mas isso garante)
            if (!_vm.IsAdmin)
            {
                await DisplayAlert("Acesso negado", "Você não tem permissão para acessar o Admin.", "OK");
                return;
            }

            // Opção A: abrir um "AdminHomePage" (recomendado se você tiver um hub)
            await Shell.Current.GoToAsync(nameof(AmoraApp.Views.Admin.AdminHomePage));

            // Opção B: abrir direto o AdminPostsPage (se já existir rota registrada)
            //await Shell.Current.GoToAsync(nameof(AmoraApp.Views.Admin.AdminPostsPage));

            // Se você não usa rotas por nome, alternativa:
            // await Navigation.PushAsync(new AmoraApp.Views.Admin.AdminPostsPage());
        }

        private async void OnChangePhotoClicked(object sender, EventArgs e)
        {
            try
            {
                // Garantir UID (sessão real)
                var user = FirebaseAuthService.Instance.GetCurrentUser();
                var uid = user?.Uid;

                if (string.IsNullOrEmpty(uid))
                {
                    await DisplayAlert("Erro", "Sessão expirada. Faça login novamente.", "OK");
                    return;
                }

                _vm.CurrentUserId = uid;

                var hasPhoto = !string.IsNullOrWhiteSpace(_vm.PhotoUrl);

                string? action;
                if (!hasPhoto)
                {
                    action = await DisplayActionSheet(
                        "Foto de perfil",
                        "Cancelar",
                        null,
                        "Galeria",
                        "Câmera");
                }
                else
                {
                    action = await DisplayActionSheet(
                        "Foto de perfil",
                        "Cancelar",
                        "Remover",
                        "Ver",
                        "Substituir pela galeria",
                        "Substituir pela câmera");
                }

                if (string.IsNullOrWhiteSpace(action) || action == "Cancelar")
                    return;

                if (action == "Ver")
                {
                    await Navigation.PushModalAsync(new PhotoPreviewPage(_vm.PhotoUrl));
                    return;
                }

                if (action == "Remover")
                {
                    _vm.PhotoUrl = "";
                    await _vm.SaveAsync();

                    if (!string.IsNullOrEmpty(_vm.ErrorMessage))
                        await DisplayAlert("Erro", _vm.ErrorMessage, "OK");

                    return;
                }

                // Substituir / adicionar
                if (action == "Galeria" || action == "Substituir pela galeria")
                {
                    var url = await PickFromGalleryAndUploadAsync(
                        $"users/{_vm.CurrentUserId}/profile_{Guid.NewGuid():N}.jpg");

                    if (!string.IsNullOrEmpty(url))
                    {
                        _vm.PhotoUrl = url;
                        await _vm.SaveAsync();
                    }
                }
                else if (action == "Câmera" || action == "Substituir pela câmera")
                {
                    var url = await CaptureFromCameraAndUploadAsync(
                        $"users/{_vm.CurrentUserId}/profile_{Guid.NewGuid():N}.jpg");

                    if (!string.IsNullOrEmpty(url))
                    {
                        _vm.PhotoUrl = url;
                        await _vm.SaveAsync();
                    }
                }

                if (!string.IsNullOrEmpty(_vm.ErrorMessage))
                    await DisplayAlert("Erro", _vm.ErrorMessage, "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", ex.Message, "OK");
            }
        }



        private async void OnExtraPhotoSlotTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not PhotoSlot slot)
                return;

            if (string.IsNullOrEmpty(_vm.CurrentUserId))
            {
                var uid = FirebaseAuthService.Instance.CurrentUserUid;
                if (string.IsNullOrEmpty(uid))
                {
                    await DisplayAlert("Erro", "Usuário não autenticado.", "OK");
                    return;
                }
                _vm.CurrentUserId = uid;
            }

            var hasPhoto = !string.IsNullOrWhiteSpace(slot.ImageUrl);

            if (!hasPhoto)
            {
                // limite por plano
                var currentPhotos = _vm.ExtraPhotoSlots.Count(s => !string.IsNullOrWhiteSpace(s.ImageUrl));
                var limit = _vm.PhotoLimit;

                if (currentPhotos >= limit)
                {
                    await DisplayAlert("Limite atingido", $"Seu plano permite até {limit} fotos.", "OK");
                    return;
                }

                var action = await DisplayActionSheet(
                    "Adicionar foto",
                    "Cancelar",
                    null,
                    "Galeria",
                    "Câmera");

                if (action == "Galeria")
                {
                    var url = await PickFromGalleryAndUploadAsync(
                        $"users/{_vm.CurrentUserId}/gallery_{slot.Index}_{Guid.NewGuid():N}.jpg");

                    if (!string.IsNullOrEmpty(url))
                    {
                        slot.ImageUrl = url;
                        await _vm.SaveAsync();
                    }
                }
                else if (action == "Câmera")
                {
                    var url = await CaptureFromCameraAndUploadAsync(
                        $"users/{_vm.CurrentUserId}/gallery_{slot.Index}_{Guid.NewGuid():N}.jpg");

                    if (!string.IsNullOrEmpty(url))
                    {
                        slot.ImageUrl = url;
                        await _vm.SaveAsync();
                    }
                }
            }
            else
            {
                var action = await DisplayActionSheet(
                    "Foto",
                    "Cancelar",
                    "Remover",
                    "Ver",
                    "Substituir pela galeria",
                    "Substituir pela câmera");

                if (action == "Remover")
                {
                    slot.ImageUrl = null;
                    await _vm.SaveAsync();
                }
                else if (action == "Ver")
                {
                    if (!string.IsNullOrWhiteSpace(slot.ImageUrl))
                        await Navigation.PushModalAsync(new PhotoPreviewPage(slot.ImageUrl));
                }
                else if (action == "Substituir pela galeria")
                {
                    var url = await PickFromGalleryAndUploadAsync(
                        $"users/{_vm.CurrentUserId}/gallery_{slot.Index}_{Guid.NewGuid():N}.jpg");

                    if (!string.IsNullOrEmpty(url))
                    {
                        slot.ImageUrl = url;
                        await _vm.SaveAsync();
                    }
                }
                else if (action == "Substituir pela câmera")
                {
                    var url = await CaptureFromCameraAndUploadAsync(
                        $"users/{_vm.CurrentUserId}/gallery_{slot.Index}_{Guid.NewGuid():N}.jpg");

                    if (!string.IsNullOrEmpty(url))
                    {
                        slot.ImageUrl = url;
                        await _vm.SaveAsync();
                    }
                }
            }
        }

        private async void OnVideoSlotTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not VideoSlot slot)
                return;

            if (string.IsNullOrEmpty(_vm.CurrentUserId))
            {
                var uid = FirebaseAuthService.Instance.CurrentUserUid;
                if (string.IsNullOrEmpty(uid))
                {
                    await DisplayAlert("Erro", "Usuário não autenticado.", "OK");
                    return;
                }
                _vm.CurrentUserId = uid;
            }

            var planService = PlanService.Instance;
            var planType = planService.ParsePlanFromString(_vm.Plan ?? "Free");

            var hasVideo = !string.IsNullOrWhiteSpace(slot.VideoUrl);

            if (!hasVideo)
            {
                if (planType != PlanType.Premium)
                {
                    await DisplayAlert(
                        "Recurso Premium",
                        "Adicionar vídeos ao perfil é exclusivo do plano Premium.\nAssine o Premium para liberar.",
                        "OK");
                    return;
                }

                var currentVideos = _vm.ExtraVideoSlots.Count(s => !string.IsNullOrWhiteSpace(s.VideoUrl));
                if (currentVideos >= 20)
                {
                    await DisplayAlert("Limite atingido", "Você já adicionou o máximo de 20 vídeos.", "OK");
                    return;
                }

                var action = await DisplayActionSheet(
                    "Adicionar vídeo",
                    "Cancelar",
                    null,
                    "Galeria de vídeos");

                if (action == "Galeria de vídeos")
                {
                    var url = await PickVideoFromGalleryAndUploadAsync(
                        $"users/{_vm.CurrentUserId}/videos/video_{slot.Index}_{Guid.NewGuid():N}.mp4");

                    if (!string.IsNullOrEmpty(url))
                    {
                        slot.VideoUrl = url;
                        await _vm.SaveAsync();
                    }
                }
            }
            else
            {
                var action = await DisplayActionSheet(
                    "Vídeo",
                    "Cancelar",
                    "Remover",
                    "Assistir",
                    "Substituir (galeria)");

                if (action == "Remover")
                {
                    slot.VideoUrl = null;
                    await _vm.SaveAsync();
                }
                else if (action == "Assistir")
                {
                    try
                    {
                        await Launcher.Default.OpenAsync(slot.VideoUrl);
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Erro", $"Não foi possível abrir o vídeo.\n{ex.Message}", "OK");
                    }
                }
                else if (action == "Substituir (galeria)")
                {
                    if (planType != PlanType.Premium)
                    {
                        await DisplayAlert(
                            "Recurso Premium",
                            "Substituir vídeos do perfil é exclusivo do plano Premium.",
                            "OK");
                        return;
                    }

                    var url = await PickVideoFromGalleryAndUploadAsync(
                        $"users/{_vm.CurrentUserId}/videos/video_{slot.Index}_{Guid.NewGuid():N}.mp4");

                    if (!string.IsNullOrEmpty(url))
                    {
                        slot.VideoUrl = url;
                        await _vm.SaveAsync();
                    }
                }
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            await _vm.SaveAsync();

            if (string.IsNullOrEmpty(_vm.ErrorMessage))
                await DisplayAlert("Pronto", "Perfil salvo com sucesso!", "OK");
            else
                await DisplayAlert("Erro", _vm.ErrorMessage, "OK");
        }

        private async Task<string?> PickFromGalleryAndUploadAsync(string fileName)
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Escolha uma foto",
                    FileTypes = FilePickerFileType.Images
                });

                if (result == null)
                    return null;

                using var stream = await result.OpenReadAsync();
                return await FirebaseStorageService.Instance.UploadFileAsync(stream, fileName, "image/jpeg");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro ao selecionar/enviar imagem", ex.Message, "OK");
                return null;
            }
        }

        private async Task<string?> CaptureFromCameraAndUploadAsync(string fileName)
        {
            try
            {
                if (!MediaPicker.Default.IsCaptureSupported)
                {
                    await DisplayAlert("Câmera não disponível", "Este dispositivo não suporta captura de fotos.", "OK");
                    return null;
                }

                var photo = await MediaPicker.Default.CapturePhotoAsync();
                if (photo == null)
                    return null;

                using var stream = await photo.OpenReadAsync();
                return await FirebaseStorageService.Instance.UploadFileAsync(stream, fileName, "image/jpeg");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro ao capturar/enviar imagem", ex.Message, "OK");
                return null;
            }
        }

        private async Task<string?> PickVideoFromGalleryAndUploadAsync(string fileName)
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Escolha um vídeo (até 15s)",
                    FileTypes = FilePickerFileType.Videos
                });

                if (result == null)
                    return null;

                using var stream = await result.OpenReadAsync();
                return await FirebaseStorageService.Instance.UploadFileAsync(stream, fileName, "video/mp4");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro ao selecionar/enviar vídeo", ex.Message, "OK");
                return null;
            }
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            var confirm = await DisplayAlert(
                "Sair",
                "Tem certeza que deseja sair da sua conta?",
                "Sair",
                "Cancelar");

            if (!confirm)
                return;

            FirebaseAuthService.Instance.Logout();

            Application.Current.MainPage =
                new NavigationPage(new LoginPage(new AuthViewModel()));
        }

        private async void OnVerifyClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(VerificationPage));
        }

        private async void OnSupportClicked(object sender, EventArgs e)
        {
            try
            {
                // Sua rota já existe no AppShell:
                // Routing.RegisterRoute(nameof(SupportPage), typeof(SupportPage));
                await Shell.Current.GoToAsync(nameof(SupportPage));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", "Não foi possível abrir o suporte.\n" + ex.Message, "OK");
            }
        }


    }
}
