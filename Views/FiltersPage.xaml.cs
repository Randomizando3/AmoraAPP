using AmoraApp.ViewModels;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views
{
    public partial class FiltersPage : ContentPage
    {
        private readonly DiscoverViewModel _discoverVm;

        // Construtor SEM parâmetros (evita erro do XAML preview)
        public FiltersPage()
        {
            InitializeComponent();
        }

        // Construtor COM ViewModel (usado pelo DiscoverPage)
        public FiltersPage(DiscoverViewModel discoverVm)
        {
            InitializeComponent();
            _discoverVm = discoverVm;
            BindingContext = _discoverVm;

            _discoverVm.SyncFilterInterestsFromFilterList();
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
        //  INTERESSES (chips)
        // ======================

        private void OnInterestTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement ve && ve.BindingContext is FilterInterestItem chip)
            {
                chip.IsSelected = !chip.IsSelected;
            }
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

            _discoverVm.ProfessionFilter = string.Empty;
            _discoverVm.EducationFilter = string.Empty;
            _discoverVm.ReligionFilter = string.Empty;
            _discoverVm.OrientationFilter = string.Empty;

            _discoverVm.InterestFilter.Clear();
            foreach (var item in _discoverVm.AvailableFilterInterests)
                item.IsSelected = false;

            UpdateGenderButtons();
        }

        // ======================
        //  APLICAR FILTROS
        // ======================

        private async void OnApplyFilterClicked(object sender, System.EventArgs e)
        {
            if (_discoverVm == null) return;

            // Atualiza lista de interesses selecionados no ViewModel
            _discoverVm.InterestFilter = _discoverVm.AvailableFilterInterests
                .Where(i => i.IsSelected)
                .Select(i => i.Name)
                .ToList();

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
