using System;
using System.IO;
using System.Threading.Tasks;
using AmoraApp.Models;
using AmoraApp.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Media;

namespace AmoraApp.Views
{
    public partial class VerificationPage : ContentPage
    {
        private byte[]? _selfieBytes;
        private byte[]? _docBytes;

        public VerificationPage()
        {
            InitializeComponent();
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnPickSelfieClicked(object sender, EventArgs e)
        {
            var bytes = await PickImageAsync("Selfie");
            if (bytes == null) return;

            _selfieBytes = bytes;
            SetPreview(SelfiePreview, SelfiePlaceholder, bytes);
        }

        private async void OnPickDocClicked(object sender, EventArgs e)
        {
            var bytes = await PickImageAsync("Documento");
            if (bytes == null) return;

            _docBytes = bytes;
            SetPreview(DocPreview, DocPlaceholder, bytes);
        }

        private async Task<byte[]?> PickImageAsync(string title)
        {
            try
            {
                var action = await DisplayActionSheet($"{title}: escolher origem", "Cancelar", null, "Galeria", "Câmera");
                if (action == "Cancelar" || string.IsNullOrWhiteSpace(action))
                    return null;

                if (action == "Galeria")
                {
                    var result = await FilePicker.PickAsync(new PickOptions
                    {
                        PickerTitle = $"Selecione {title}",
                        FileTypes = FilePickerFileType.Images
                    });

                    if (result == null) return null;

                    using var stream = await result.OpenReadAsync();
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    return ms.ToArray();
                }

                if (action == "Câmera")
                {
                    if (!MediaPicker.Default.IsCaptureSupported)
                    {
                        await DisplayAlert("Câmera", "Este dispositivo não suporta captura de fotos.", "OK");
                        return null;
                    }

                    var photo = await MediaPicker.Default.CapturePhotoAsync();
                    if (photo == null) return null;

                    using var stream = await photo.OpenReadAsync();
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    return ms.ToArray();
                }

                return null;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", "Falha ao selecionar imagem: " + ex.Message, "OK");
                return null;
            }
        }

        private void SetPreview(Image img, Label placeholder, byte[] data)
        {
            var cachePath = Path.Combine(FileSystem.CacheDirectory, $"ver_{Guid.NewGuid():N}.jpg");
            File.WriteAllBytes(cachePath, data);

            img.Source = ImageSource.FromFile(cachePath);
            img.IsVisible = true;
            placeholder.IsVisible = false;
        }

        private async void OnSubmitClicked(object sender, EventArgs e)
        {
            if (_selfieBytes == null || _docBytes == null)
            {
                await DisplayAlert("Faltando arquivo", "Selecione a selfie e o documento antes de enviar.", "OK");
                return;
            }

            try
            {
                SubmitBtn.IsEnabled = false;
                StatusLabel.Text = "Enviando...";

                var uid = FirebaseAuthService.Instance.CurrentUserUid;
                if (string.IsNullOrWhiteSpace(uid))
                {
                    await DisplayAlert("Sessão", "Você precisa estar logado.", "OK");
                    return;
                }

                var db = FirebaseDatabaseService.Instance;
                var profile = await db.GetUserProfileAsync(uid) ?? new UserProfile { Id = uid };

                // ===== Upload dentro de users/{uid}/... (bate nas suas rules atuais) =====
                string selfieUrl;
                using (var ms = new MemoryStream(_selfieBytes))
                {
                    var path = $"users/{uid}/verification/selfie_{Guid.NewGuid():N}.jpg";
                    selfieUrl = await FirebaseStorageService.Instance.UploadImageAsync(ms, path) ?? "";
                }

                string docUrl;
                using (var ms = new MemoryStream(_docBytes))
                {
                    var path = $"users/{uid}/verification/document_{Guid.NewGuid():N}.jpg";
                    docUrl = await FirebaseStorageService.Instance.UploadImageAsync(ms, path) ?? "";
                }

                var req = new VerificationRequest
                {
                    Id = uid,
                    UserId = uid,
                    UserName = profile.DisplayName ?? "",
                    UserEmail = profile.Email ?? "",
                    UserPhotoUrl = profile.PhotoUrl ?? "",
                    SelfieUrl = selfieUrl,
                    DocumentUrl = docUrl,
                    Status = "pending",
                    CreatedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                await db.CreateOrUpdateVerificationAsync(req);

                // espelha status no perfil
                profile.VerificationStatus = "pending";
                profile.VerificationSubmittedAtUtcMs = req.CreatedAtUtcMs;
                profile.IsVerified = false;
                await db.SaveUserProfileAsync(profile);

                StatusLabel.Text = "Enviado. Seu pedido está em análise.";
                await DisplayAlert("Enviado", "Sua verificação foi enviada e está em análise.", "OK");
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", "Falha ao enviar verificação: " + ex.Message, "OK");
            }
            finally
            {
                SubmitBtn.IsEnabled = true;
            }
        }
    }
}
