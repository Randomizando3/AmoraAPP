using System;
using System.Threading.Tasks;
using AmoraApp.Models;
using AmoraApp.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace AmoraApp.Views
{
    public partial class DiscoverPage : ContentPage
    {
        private DiscoverViewModel Vm => BindingContext as DiscoverViewModel;

        private double _lastSwipeTotalX = 0;
        private bool _requestedOnce = false;

        public DiscoverPage()
            : this(new DiscoverViewModel())
        {
        }

        public DiscoverPage(DiscoverViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }

        public DiscoverPage(UserProfile singleUser)
            : this(new DiscoverViewModel(singleUser))
        {
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                // Pede permissões 1 vez (pra não ficar repetindo)
                if (!_requestedOnce)
                {
                    _requestedOnce = true;
                    await EnsurePermissionsAsync();
                }

                if (Vm != null)
                    await Vm.InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DiscoverPage] Erro em OnAppearing: {ex}");
            }
        }

        private static async Task EnsurePermissionsAsync()
        {
            try
            {
                // Localização (Discover costuma usar distância/filtro)
                var locStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (locStatus != PermissionStatus.Granted)
                    await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DiscoverPage] Location permission error: {ex}");
            }

            try
            {
                // Se você abre câmera dentro do app, é melhor pedir aqui (ou no momento do uso)
                var camStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (camStatus != PermissionStatus.Granted)
                    await Permissions.RequestAsync<Permissions.Camera>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DiscoverPage] Camera permission error: {ex}");
            }

            try
            {
                // Se você grava áudio/enviar áudio
                var micStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
                if (micStatus != PermissionStatus.Granted)
                    await Permissions.RequestAsync<Permissions.Microphone>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DiscoverPage] Microphone permission error: {ex}");
            }
        }

        // ================== SWIPE NO CARD ==================

        private void OnSwipe(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Running:
                    _lastSwipeTotalX = e.TotalX;

                    SwipeCard.TranslationX = e.TotalX;
                    SwipeCard.TranslationY = e.TotalY * 0.2;
                    SwipeCard.Rotation = e.TotalX / 25;
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    HandleSwipeEnd(_lastSwipeTotalX);
                    _lastSwipeTotalX = 0;
                    break;
            }
        }

        private async void HandleSwipeEnd(double totalX)
        {
            const int threshold = 80;

            if (totalX > threshold)
                await SwipeRightAsync();
            else if (totalX < -threshold)
                await SwipeLeftAsync();
            else
                await ResetCardAsync();
        }

        private async Task SwipeLeftAsync()
        {
            await SwipeCard.TranslateTo(-500, 0, 150, Easing.Linear);
            SwipeCard.Opacity = 0;

            if (Vm?.DislikeCommand != null && Vm.DislikeCommand.CanExecute(null))
                Vm.DislikeCommand.Execute(null);

            await ResetCardAsync();
        }

        private async Task SwipeRightAsync()
        {
            await SwipeCard.TranslateTo(500, 0, 150, Easing.Linear);
            SwipeCard.Opacity = 0;

            if (Vm?.LikeCommand != null && Vm.LikeCommand.CanExecute(null))
                Vm.LikeCommand.Execute(null);

            await ResetCardAsync();
        }

        private async Task ResetCardAsync()
        {
            SwipeCard.TranslationX = 0;
            SwipeCard.TranslationY = 0;
            SwipeCard.Rotation = 0;
            SwipeCard.Opacity = 1;
            await Task.CompletedTask;
        }

        private async void OnCardTapped(object sender, EventArgs e)
        {
            if (Vm?.CurrentUser != null)
            {
                var user = Vm.CurrentUser;
                await Navigation.PushModalAsync(new PhotoGalleryPage(user));
            }
        }

        private async void OnFilterHeaderClicked(object sender, EventArgs e)
        {
            if (Vm != null)
                await Navigation.PushAsync(new FiltersPage(Vm));
        }

        private async void OnRewindTapped(object sender, EventArgs e)
        {
            if (Vm?.RewindCommand != null && Vm.RewindCommand.CanExecute(null))
                Vm.RewindCommand.Execute(null);

            await Task.CompletedTask;
        }

        private async void OnBoostTapped(object sender, EventArgs e)
        {
            if (Vm?.BoostCommand != null && Vm.BoostCommand.CanExecute(null))
                Vm.BoostCommand.Execute(null);

            await Task.CompletedTask;
        }

        private async void OnAddTapped(object sender, EventArgs e)
        {
            await DisplayAlert("Info", "O botão ADD usa o AddFriendCommand no ViewModel.", "OK");
        }
    }
}
