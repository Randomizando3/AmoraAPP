using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using AmoraApp.Models;
using AmoraApp.Services;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views.Admin
{
    public partial class AdminVerificationsPage : ContentPage
    {
        public ObservableCollection<VerificationRequest> Items { get; } = new();
        public string StatusText { get; set; } = "Carregando...";

        public ICommand ApproveCommand { get; }
        public ICommand RejectCommand { get; }

        public AdminVerificationsPage()
        {
            InitializeComponent();
            BindingContext = this;

            ApproveCommand = new Command<VerificationRequest>(async (v) => await DecideAsync(v, "approved"));
            RejectCommand = new Command<VerificationRequest>(async (v) => await DecideAsync(v, "rejected"));
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
                StatusText = "Carregando verificações...";
                OnPropertyChanged(nameof(StatusText));

                Items.Clear();
                var list = await FirebaseDatabaseService.Instance.GetAllVerificationsAsync(1000);
                foreach (var it in list)
                    Items.Add(it);

                StatusText = $"Total: {Items.Count}";
                OnPropertyChanged(nameof(StatusText));
            }
            catch (Exception ex)
            {
                StatusText = "Erro: " + ex.Message;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        private async Task DecideAsync(VerificationRequest v, string status)
        {
            if (v == null) return;

            var note = await DisplayPromptAsync("Nota (opcional)", status == "approved" ? "Aprovar verificação?" : "Rejeitar verificação?", "OK", "Cancelar");
            if (note == null) return;

            try
            {
                await FirebaseDatabaseService.Instance.UpdateVerificationStatusAsync(v.UserId, status, note ?? "");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", ex.Message, "OK");
            }
        }

        private async void OnBackTapped(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
        private async void OnRefreshTapped(object sender, EventArgs e) => await LoadAsync();
    }
}
