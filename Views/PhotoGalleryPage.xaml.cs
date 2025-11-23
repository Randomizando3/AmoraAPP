using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AmoraApp.Models;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views
{
    public partial class PhotoGalleryPage : ContentPage
    {
        // Snapshot do usuário na hora que abriu a página
        public UserProfile User { get; }

        // Lista final de fotos (foto principal + extras válidas, sem duplicar)
        public ObservableCollection<string> Photos { get; } = new();

        public PhotoGalleryPage(UserProfile user)
        {
            InitializeComponent();

            // Cópia simples pra não ficar dependendo do Discover em tempo real
            User = new UserProfile
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email,
                Bio = user.Bio,
                JobTitle = user.JobTitle,
                EducationLevel = user.EducationLevel,
                EducationInstitution = user.EducationInstitution,
                City = user.City,
                Age = user.Age,
                Gender = user.Gender,
                SexualOrientation = user.SexualOrientation,
                PhotoUrl = user.PhotoUrl,
                Gallery = user.Gallery?.ToList() ?? new List<string>(),
                Interests = user.Interests?.ToList() ?? new List<string>()
            };

            BindingContext = this;

            BuildPhotosList();
        }

        private void BuildPhotosList()
        {
            Photos.Clear();

            // 1) Foto principal, se existir
            if (!string.IsNullOrWhiteSpace(User.PhotoUrl))
                Photos.Add(User.PhotoUrl);

            // 2) Fotos adicionais distintas da principal
            if (User.Gallery != null)
            {
                foreach (var url in User.Gallery)
                {
                    if (string.IsNullOrWhiteSpace(url))
                        continue;

                    if (!Photos.Contains(url))
                        Photos.Add(url);
                }
            }

            // Se não tiver nada, você pode adicionar um placeholder aqui se quiser
            // if (Photos.Count == 0) Photos.Add("placeholder_profile.png");
        }

        private async void OnCloseTapped(object sender, System.EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        private async void OnInfoFabTapped(object sender, System.EventArgs e)
        {
            // Rola até a seção de informações
            if (InfoSection != null && RootScroll != null)
            {
                await RootScroll.ScrollToAsync(InfoSection, ScrollToPosition.Start, true);
            }
        }
    }
}
