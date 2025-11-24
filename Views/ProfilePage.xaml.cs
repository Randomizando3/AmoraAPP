using System;
using System.IO;
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
            // Atualiza localização atual (GPS / IP / debug-SP)
            await _vm.UpdateLocationAsync();
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            if (Navigation.NavigationStack.Count > 1)
                await Navigation.PopAsync();
        }

        // Tap no fundo da página → tira foco e fecha dropdowns
        private void OnRootTapped(object sender, EventArgs e)
        {
            if (CityEntry.IsFocused)
                CityEntry.Unfocus();

            if (JobEntry.IsFocused)
                JobEntry.Unfocus();

            _vm.JobSuggestions.Clear();
            _vm.IsJobSuggestionsVisible = false;
        }

        // ======== PROFISSÃO / AUTOCOMPLETE ========

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

        // ======== FOTO PRINCIPAL ========

        private async void OnChangePhotoClicked(object sender, EventArgs e)
        {
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

            var action = await DisplayActionSheet(
                "Foto de perfil",
                "Cancelar",
                null,
                "Galeria",
                "Câmera");

            if (action == "Galeria")
            {
                var url = await PickFromGalleryAndUploadAsync(
                    $"users/{_vm.CurrentUserId}/profile_{Guid.NewGuid():N}.jpg");

                if (!string.IsNullOrEmpty(url))
                {
                    _vm.PhotoUrl = url;
                    await _vm.SaveAsync();
                }
            }
            else if (action == "Câmera")
            {
                var url = await CaptureFromCameraAndUploadAsync(
                    $"users/{_vm.CurrentUserId}/profile_{Guid.NewGuid():N}.jpg");

                if (!string.IsNullOrEmpty(url))
                {
                    _vm.PhotoUrl = url;
                    await _vm.SaveAsync();
                }
            }
        }

        // ======== FOTOS EXTRAS (30 slots) ========

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
                var currentPhotos = _vm.ExtraPhotoSlots.Count(s => !string.IsNullOrWhiteSpace(s.ImageUrl));
                if (currentPhotos >= 30)
                {
                    await DisplayAlert("Limite atingido", "Você já adicionou o máximo de 30 fotos.", "OK");
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
                    try
                    {
                        await Launcher.Default.OpenAsync(slot.ImageUrl);
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Erro", $"Não foi possível abrir a foto.\n{ex.Message}", "OK");
                    }
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

        // ======== VÍDEOS (20 slots) ========

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

            var hasVideo = !string.IsNullOrWhiteSpace(slot.VideoUrl);

            if (!hasVideo)
            {
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

        // ======== SALVAR ========

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            await _vm.SaveAsync();

            if (string.IsNullOrEmpty(_vm.ErrorMessage))
                await DisplayAlert("Pronto", "Perfil salvo com sucesso!", "OK");
            else
                await DisplayAlert("Erro", _vm.ErrorMessage, "OK");
        }

        // ======== HELPERS UPLOAD (IMAGEM) ========

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
                var url = await FirebaseStorageService.Instance.UploadImageAsync(stream, fileName);
                return url;
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
                var url = await FirebaseStorageService.Instance.UploadImageAsync(stream, fileName);
                return url;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro ao capturar/enviar imagem", ex.Message, "OK");
                return null;
            }
        }

        // ======== HELPERS UPLOAD (VÍDEO) ========

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
                // Reutilizando o mesmo serviço (nome "Image" mas aceita stream)
                var url = await FirebaseStorageService.Instance.UploadImageAsync(stream, fileName);
                return url;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro ao selecionar/enviar vídeo", ex.Message, "OK");
                return null;
            }
        }
    }
}
