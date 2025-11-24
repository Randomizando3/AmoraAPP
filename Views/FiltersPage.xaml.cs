using System.Linq;
using AmoraApp.ViewModels;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views
{
    public partial class FiltersPage : ContentPage
    {
        private readonly DiscoverViewModel _discoverVm;

        // Construtor SEM parâmetros (preview)
        public FiltersPage()
        {
            InitializeComponent();
        }

        // Construtor COM ViewModel (usado a partir do Discover)
        public FiltersPage(DiscoverViewModel discoverVm)
        {
            InitializeComponent();
            _discoverVm = discoverVm;
            BindingContext = _discoverVm;

            UpdateGenderButtons();

            // Restaura visual dos interesses já selecionados
            Device.BeginInvokeOnMainThread(() =>
            {
                if (_discoverVm.SelectedInterestFilters.Count == 0)
                    return;

                if (InterestsCollectionView.SelectedItems != null)
                    InterestsCollectionView.SelectedItems.Clear();

                foreach (var interest in _discoverVm
                             .SelectedInterestFilters
                             .ToList())
                {
                    InterestsCollectionView.SelectedItems.Add(interest);
                }
            });
        }

        // ======================
        //     GÊNERO
        // ======================

        private void OnGirlsClicked(object sender, System.EventArgs e)
        {
            if (_discoverVm == null) return;
            _discoverVm.GenderFilter = "Girls";
            UpdateGenderButtons();
        }

        private void OnBoysClicked(object sender, System.EventArgs e)
        {
            if (_discoverVm == null) return;
            _discoverVm.GenderFilter = "Boys";
            UpdateGenderButtons();
        }

        private void OnBothClicked(object sender, System.EventArgs e)
        {
            if (_discoverVm == null) return;
            _discoverVm.GenderFilter = "Both";
            UpdateGenderButtons();
        }

        private void UpdateGenderButtons()
        {
            if (_discoverVm == null)
                return;

            GirlsButton.BackgroundColor =
                _discoverVm.GenderFilter == "Girls"
                    ? Color.FromHex("#FF4E8A")
                    : Color.FromHex("#F5F5F5");
            GirlsButton.TextColor =
                _discoverVm.GenderFilter == "Girls"
                    ? Colors.White
                    : Color.FromHex("#555555");

            BoysButton.BackgroundColor =
                _discoverVm.GenderFilter == "Boys"
                    ? Color.FromHex("#FF4E8A")
                    : Color.FromHex("#F5F5F5");
            BoysButton.TextColor =
                _discoverVm.GenderFilter == "Boys"
                    ? Colors.White
                    : Color.FromHex("#555555");

            BothButton.BackgroundColor =
                _discoverVm.GenderFilter == "Both"
                    ? Color.FromHex("#FF4E8A")
                    : Color.FromHex("#F5F5F5");
            BothButton.TextColor =
                _discoverVm.GenderFilter == "Both"
                    ? Colors.White
                    : Color.FromHex("#555555");
        }

        // ======================
        //  RESETAR FILTROS
        // ======================

        private void OnResetClicked(object sender, System.EventArgs e)
        {
            if (_discoverVm == null) return;

            // Gênero / idade / distância
            _discoverVm.GenderFilter = "Both";
            _discoverVm.MaxAgeFilter = 35;
            _discoverVm.DistanceFilterKm = 20;

            // Campos de texto
            _discoverVm.ProfessionFilter = string.Empty;
            _discoverVm.ReligionFilter = string.Empty;
            _discoverVm.EducationFilter = string.Empty;
            _discoverVm.OrientationFilter = string.Empty;

            // Interesses (VM + UI)
            _discoverVm.ClearInterestFilters();
            InterestsCollectionView.SelectedItems?.Clear();

            UpdateGenderButtons();
        }

        // ======================
        //  APLICAR FILTROS
        // ======================

        private async void OnApplyFilterClicked(object sender, System.EventArgs e)
        {
            if (_discoverVm == null) return;

            // Atualiza a lista de interesses selecionados no VM
            _discoverVm.ClearInterestFilters();

            if (InterestsCollectionView.SelectedItems != null)
            {
                foreach (var item in InterestsCollectionView.SelectedItems)
                {
                    if (item is string interest && !string.IsNullOrWhiteSpace(interest))
                        _discoverVm.AddInterestFilter(interest);
                }
            }

            // Aplica filtros na pilha de perfis
            _discoverVm.ApplyFilters();

            await Navigation.PopAsync();
        }

        // ======================
        //  FECHAR
        // ======================

        private async void OnCloseTapped(object sender, System.EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}
