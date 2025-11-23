using AmoraApp.ViewModels;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views
{
    public partial class FiltersPage : ContentPage
    {
        private readonly DiscoverViewModel _discoverVm;

        // 🔹 Construtor SEM parâmetros (evita erro CS7036)
        public FiltersPage()
        {
            InitializeComponent();
        }

        // 🔹 Construtor COM ViewModel (ideal quando chamado pelo Discover)
        public FiltersPage(DiscoverViewModel discoverVm)
        {
            InitializeComponent();
            _discoverVm = discoverVm;
            BindingContext = _discoverVm;

            UpdateGenderButtons();
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

            // Girls
            GirlsButton.BackgroundColor =
                _discoverVm.GenderFilter == "Girls" ? Color.FromHex("#FF4E8A") : Color.FromHex("#F5F5F5");
            GirlsButton.TextColor =
                _discoverVm.GenderFilter == "Girls" ? Colors.White : Color.FromHex("#555555");

            // Boys
            BoysButton.BackgroundColor =
                _discoverVm.GenderFilter == "Boys" ? Color.FromHex("#FF4E8A") : Color.FromHex("#F5F5F5");
            BoysButton.TextColor =
                _discoverVm.GenderFilter == "Boys" ? Colors.White : Color.FromHex("#555555");

            // Both
            BothButton.BackgroundColor =
                _discoverVm.GenderFilter == "Both" ? Color.FromHex("#FF4E8A") : Color.FromHex("#F5F5F5");
            BothButton.TextColor =
                _discoverVm.GenderFilter == "Both" ? Colors.White : Color.FromHex("#555555");
        }

        // ======================
        //  RESETAR FILTROS
        // ======================

        private void OnResetClicked(object sender, System.EventArgs e)
        {
            if (_discoverVm == null) return;

            _discoverVm.GenderFilter = "Both";
            _discoverVm.MaxAgeFilter = 35;
            _discoverVm.DistanceFilterKm = 20;

            UpdateGenderButtons();
        }

        // ======================
        //  APLICAR FILTROS
        // ======================

        private async void OnApplyFilterClicked(object sender, System.EventArgs e)
        {
            if (_discoverVm == null) return;

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
