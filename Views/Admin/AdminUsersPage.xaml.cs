using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AmoraApp.Models;
using AmoraApp.Services;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views.Admin
{
    public partial class AdminUsersPage : ContentPage
    {
        private List<UserProfile> _all = new();

        public ObservableCollection<UserProfile> Items { get; } = new();
        public string StatusText { get; set; } = "Carregando...";
        public string SearchText { get; set; } = "";

        public ICommand PlanCommand { get; }
        public ICommand TokensCommand { get; }
        public ICommand VerifyCommand { get; }
        public ICommand DeleteCommand { get; }

        public AdminUsersPage()
        {
            InitializeComponent();
            BindingContext = this;

            PlanCommand = new Command<UserProfile>(async (u) => await ChangePlanAsync(u));
            TokensCommand = new Command<UserProfile>(async (u) => await ChangeTokensAsync(u));
            VerifyCommand = new Command<UserProfile>(async (u) => await ChangeVerificationAsync(u));
            DeleteCommand = new Command<UserProfile>(async (u) => await DeleteAsync(u));
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (!await AdminUi.GuardAsync()) return;
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                StatusText = "Carregando usuários...";
                OnPropertyChanged(nameof(StatusText));

                _all = (await FirebaseDatabaseService.Instance.GetAllUsersAsync(1500)).ToList();
                ApplyFilter();

                StatusText = $"Total: {_all.Count} | Exibindo: {Items.Count}";
                OnPropertyChanged(nameof(StatusText));
            }
            catch (Exception ex)
            {
                StatusText = "Erro: " + ex.Message;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        private void ApplyFilter()
        {
            Items.Clear();

            var q = (SearchText ?? "").Trim();
            IEnumerable<UserProfile> list = _all;

            if (!string.IsNullOrWhiteSpace(q))
            {
                list = list.Where(u =>
                    (u.DisplayName ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (u.Id ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var u in list.Take(300))
                Items.Add(u);
        }

        private async Task ChangePlanAsync(UserProfile u)
        {
            if (u == null) return;

            var pick = await DisplayActionSheet("Plano", "Cancelar", null, "Free", "Plus", "Premium");
            if (pick == null || pick == "Cancelar") return;

            try
            {
                await FirebaseDatabaseService.Instance.UpdateUserPlanAsync(u.Id, pick);
                u.Plan = pick;
                await LoadAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", ex.Message, "OK");
            }
        }

        private async Task ChangeTokensAsync(UserProfile u)
        {
            if (u == null) return;

            var val = await DisplayPromptAsync("Boost tokens", "Digite um número (ex.: 0, 1, 10)", "OK", "Cancelar", keyboard: Keyboard.Numeric);
            if (val == null) return;

            if (!int.TryParse(val, out var tokens)) tokens = 0;

            try
            {
                await FirebaseDatabaseService.Instance.UpdateUserBoostTokensAsync(u.Id, tokens);
                await DisplayAlert("OK", "Atualizado.", "OK");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", ex.Message, "OK");
            }
        }

        private async Task ChangeVerificationAsync(UserProfile u)
        {
            if (u == null) return;

            var pick = await DisplayActionSheet("Verificação", "Cancelar", null, "approved", "pending", "rejected", "none");
            if (pick == null || pick == "Cancelar") return;

            try
            {
                if (pick == "approved")
                    await FirebaseDatabaseService.Instance.SetUserVerifiedAsync(u.Id, true, "approved");
                else if (pick == "pending")
                    await FirebaseDatabaseService.Instance.SetUserVerifiedAsync(u.Id, false, "pending");
                else if (pick == "rejected")
                    await FirebaseDatabaseService.Instance.SetUserVerifiedAsync(u.Id, false, "rejected");
                else
                    await FirebaseDatabaseService.Instance.SetUserVerifiedAsync(u.Id, false, "none");

                await LoadAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", ex.Message, "OK");
            }
        }

        private async Task DeleteAsync(UserProfile u)
        {
            if (u == null) return;

            var ok = await DisplayAlert("Deletar", "Confirma deletar o perfil deste usuário (RTDB)?", "Deletar", "Cancelar");
            if (!ok) return;

            try
            {
                await FirebaseDatabaseService.Instance.DeleteUserProfileAsync(u.Id);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", ex.Message, "OK");
            }
        }

        private async void OnSearchCompleted(object sender, EventArgs e)
        {
            ApplyFilter();
            StatusText = $"Total: {_all.Count} | Exibindo: {Items.Count}";
            OnPropertyChanged(nameof(StatusText));
            await Task.CompletedTask;
        }

        private async void OnBackTapped(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
        private async void OnRefreshTapped(object sender, EventArgs e) => await LoadAsync();
    }
}
