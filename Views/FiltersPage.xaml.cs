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

            // Restaura visual dos interesses / orientação / busco por já selecionados
            Device.BeginInvokeOnMainThread(() =>
            {
                // INTERESSES
                if (InterestsCollectionView.SelectedItems != null)
                    InterestsCollectionView.SelectedItems.Clear();

                if (_discoverVm.SelectedInterestFilters.Count > 0)
                {
                    foreach (var interest in _discoverVm
                                 .SelectedInterestFilters
                                 .ToList())
                    {
                        InterestsCollectionView.SelectedItems.Add(interest);
                    }
                }

                // ORIENTAÇÃO SEXUAL
                if (OrientationsCollectionView.SelectedItems != null)
                    OrientationsCollectionView.SelectedItems.Clear();

                if (_discoverVm.SelectedOrientationFilters.Count > 0)
                {
                    foreach (var ori in _discoverVm
                                 .SelectedOrientationFilters
                                 .ToList())
                    {
                        OrientationsCollectionView.SelectedItems.Add(ori);
                    }
                }

                // BUSCO POR (amizade / etc.)
                if (LookingForCollectionView.SelectedItems != null)
                    LookingForCollectionView.SelectedItems.Clear();

                if (_discoverVm.SelectedLookingForFilters.Count > 0)
                {
                    foreach (var goal in _discoverVm
                                 .SelectedLookingForFilters
                                 .ToList())
                    {
                        LookingForCollectionView.SelectedItems.Add(goal);
                    }
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
                    ? Color.FromHex("#5d259c")
                    : Color.FromHex("#F5F5F5");
            GirlsButton.TextColor =
                _discoverVm.GenderFilter == "Girls"
                    ? Colors.White
                    : Color.FromHex("#555555");

            BoysButton.BackgroundColor =
                _discoverVm.GenderFilter == "Boys"
                    ? Color.FromHex("#5d259c")
                    : Color.FromHex("#F5F5F5");
            BoysButton.TextColor =
                _discoverVm.GenderFilter == "Boys"
                    ? Colors.White
                    : Color.FromHex("#555555");

            BothButton.BackgroundColor =
                _discoverVm.GenderFilter == "Both"
                    ? Color.FromHex("#5d259c")
                    : Color.FromHex("#F5F5F5");
            BothButton.TextColor =
                _discoverVm.GenderFilter == "Both"
                    ? Colors.White
                    : Color.FromHex("#555555");
        }

        // ======================
        //  SELEÇÃO INTERESSES
        // ======================

        private void OnInterestsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_discoverVm == null) return;

            _discoverVm.ClearInterestFilters();

            var cv = (CollectionView)sender;
            if (cv.SelectedItems == null) return;

            foreach (var item in cv.SelectedItems)
            {
                if (item is string interest && !string.IsNullOrWhiteSpace(interest))
                {
                    _discoverVm.AddInterestFilter(interest);
                }
            }
        }

        // ======================
        //  SELEÇÃO ORIENTAÇÃO
        // ======================

        private void OnOrientationsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_discoverVm == null) return;

            _discoverVm.ClearOrientationFilters();

            var cv = (CollectionView)sender;
            if (cv.SelectedItems == null) return;

            foreach (var item in cv.SelectedItems)
            {
                if (item is string ori && !string.IsNullOrWhiteSpace(ori))
                {
                    _discoverVm.AddOrientationFilter(ori);
                }
            }
        }

        // ======================
        //  SELEÇÃO "BUSCO POR"
        // ======================

        private void OnLookingForSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_discoverVm == null) return;

            _discoverVm.ClearLookingForFilters();

            var cv = (CollectionView)sender;
            if (cv.SelectedItems == null) return;

            foreach (var item in cv.SelectedItems)
            {
                if (item is string goal && !string.IsNullOrWhiteSpace(goal))
                {
                    _discoverVm.AddLookingForFilter(goal);
                }
            }
        }

        // ======================
        //  RESETAR FILTROS
        // ======================

        private void OnResetClicked(object sender, System.EventArgs e)
        {
            if (_discoverVm == null) return;

            // Gênero / idade / distância
            _discoverVm.GenderFilter = "Both";
            _discoverVm.MinAgeFilter = 18;
            _discoverVm.MaxAgeFilter = 35;
            _discoverVm.DistanceFilterKm = 20;

            // Campos de texto
            _discoverVm.ProfessionFilter = string.Empty;
            _discoverVm.ReligionFilter = string.Empty;
            _discoverVm.EducationFilter = string.Empty;

            // Orientação sexual (VM + UI)
            _discoverVm.ClearOrientationFilters();
            OrientationsCollectionView.SelectedItems?.Clear();

            // Interesses (VM + UI)
            _discoverVm.ClearInterestFilters();
            InterestsCollectionView.SelectedItems?.Clear();

            // Busco por (VM + UI)
            _discoverVm.ClearLookingForFilters();
            LookingForCollectionView.SelectedItems?.Clear();

            UpdateGenderButtons();
        }

        // ======================
        //  APLICAR FILTROS
        // ======================

        private async void OnApplyFilterClicked(object sender, System.EventArgs e)
        {
            if (_discoverVm == null) return;

            // Sincroniza INTERESSES multi do CollectionView → VM
            _discoverVm.ClearInterestFilters();
            if (InterestsCollectionView.SelectedItems != null)
            {
                foreach (var item in InterestsCollectionView.SelectedItems)
                {
                    if (item is string interest && !string.IsNullOrWhiteSpace(interest))
                        _discoverVm.AddInterestFilter(interest);
                }
            }

            // Sincroniza ORIENTAÇÃO multi do CollectionView → VM
            _discoverVm.ClearOrientationFilters();
            if (OrientationsCollectionView.SelectedItems != null)
            {
                foreach (var item in OrientationsCollectionView.SelectedItems)
                {
                    if (item is string ori && !string.IsNullOrWhiteSpace(ori))
                        _discoverVm.AddOrientationFilter(ori);
                }
            }

            // Sincroniza "BUSCO POR" multi do CollectionView → VM
            _discoverVm.ClearLookingForFilters();
            if (LookingForCollectionView.SelectedItems != null)
            {
                foreach (var item in LookingForCollectionView.SelectedItems)
                {
                    if (item is string goal && !string.IsNullOrWhiteSpace(goal))
                        _discoverVm.AddLookingForFilter(goal);
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
