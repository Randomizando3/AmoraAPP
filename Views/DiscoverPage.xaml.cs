using System;
using System.Threading.Tasks;
using AmoraApp.ViewModels;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views
{
    public partial class DiscoverPage : ContentPage
    {
        private DiscoverViewModel Vm => BindingContext as DiscoverViewModel;

        // Guarda o último TotalX do pan para usar no Completed
        private double _lastSwipeTotalX = 0;

        // Construtor SEM parâmetros – usado pelo XAML / Shell
        public DiscoverPage()
            : this(new DiscoverViewModel())
        {
        }

        // Construtor COM ViewModel – se você quiser injetar manualmente
        public DiscoverPage(DiscoverViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (Vm != null)
            {
                try
                {
                    await Vm.InitializeAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DiscoverPage] Erro em OnAppearing: {ex}");
                    // Não relança: evita derrubar o app
                }
            }
        }

        // ================== SWIPE NO CARD ==================

        private void OnSwipe(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Running:
                    // Atualiza posição visual
                    _lastSwipeTotalX = e.TotalX;

                    SwipeCard.TranslationX = e.TotalX;
                    SwipeCard.TranslationY = e.TotalY * 0.2; // menos vertical
                    SwipeCard.Rotation = e.TotalX / 25;
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    HandleSwipeEnd(_lastSwipeTotalX);
                    // reseta o cache pra próxima vez
                    _lastSwipeTotalX = 0;
                    break;
            }
        }

        private async void HandleSwipeEnd(double totalX)
        {
            const int threshold = 80; // mais sensível

            if (totalX > threshold)
            {
                await SwipeRightAsync();
            }
            else if (totalX < -threshold)
            {
                await SwipeLeftAsync();
            }
            else
            {
                // Volta pro centro
                await ResetCardAsync();
            }
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

        // TAP → abre galeria de fotos do usuário
        private async void OnCardTapped(object sender, EventArgs e)
        {
            if (Vm?.CurrentUser != null)
            {
                var user = Vm.CurrentUser;
                await Navigation.PushModalAsync(new PhotoGalleryPage(user));
            }
        }

        // HEADER: filtro
        private async void OnFilterHeaderClicked(object sender, EventArgs e)
        {
            if (Vm != null)
                await Navigation.PushAsync(new FiltersPage(Vm));
        }

        // BOTÃO REWIND (se em algum lugar você ligar no XAML por Tap)
        private async void OnRewindTapped(object sender, EventArgs e)
        {
            if (Vm?.RewindCommand != null && Vm.RewindCommand.CanExecute(null))
                Vm.RewindCommand.Execute(null);

            await Task.CompletedTask;
        }

        // BOTÃO BOOST
        private async void OnBoostTapped(object sender, EventArgs e)
        {
            await DisplayAlert("Boost", "Função de boost / destaque ainda será implementada.", "OK");
        }

        // BOTÃO ADD no rodapé (amizade via Command já está no XAML)
        private async void OnAddTapped(object sender, EventArgs e)
        {
            await DisplayAlert("Info", "O botão ADD usa o AddFriendCommand no ViewModel.", "OK");
        }
    }
}
