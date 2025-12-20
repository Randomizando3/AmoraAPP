using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using AmoraApp.Models;
using AmoraApp.Services;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views.Admin
{
    public partial class AdminTicketsPage : ContentPage
    {
        public ObservableCollection<SupportTicket> Items { get; } = new();
        public string StatusText { get; set; } = "Carregando...";

        public ICommand AnswerCommand { get; }
        public ICommand CloseCommand { get; }

        public AdminTicketsPage()
        {
            InitializeComponent();
            BindingContext = this;

            AnswerCommand = new Command<SupportTicket>(async (t) => await AnswerAsync(t));
            CloseCommand = new Command<SupportTicket>(async (t) => await CloseAsync(t));
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
                StatusText = "Carregando tickets...";
                OnPropertyChanged(nameof(StatusText));

                Items.Clear();
                var list = await FirebaseDatabaseService.Instance.GetAllSupportTicketsAsync(500);
                foreach (var it in list) Items.Add(it);

                StatusText = $"Total: {Items.Count}";
                OnPropertyChanged(nameof(StatusText));
            }
            catch (Exception ex)
            {
                StatusText = "Erro: " + ex.Message;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        private async Task AnswerAsync(SupportTicket t)
        {
            if (t == null) return;

            var answer = await DisplayPromptAsync("Responder ticket", "Digite a resposta:", "Enviar", "Cancelar", maxLength: 600);
            if (answer == null) return;

            try
            {
                await FirebaseDatabaseService.Instance.UpdateSupportTicketStatusAsync(t.Id, "answered", answer);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", ex.Message, "OK");
            }
        }

        private async Task CloseAsync(SupportTicket t)
        {
            if (t == null) return;

            var ok = await DisplayAlert("Fechar", "Confirma fechar este ticket?", "Fechar", "Cancelar");
            if (!ok) return;

            try
            {
                await FirebaseDatabaseService.Instance.UpdateSupportTicketStatusAsync(t.Id, "closed", t.Answer ?? "");
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
