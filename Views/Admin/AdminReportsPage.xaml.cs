using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AmoraApp.Models;
using AmoraApp.Services;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views.Admin
{
    public partial class AdminReportsPage : ContentPage
    {
        public ObservableCollection<ReportItem> Items { get; } = new();
        public string StatusText { get; set; } = "Carregando...";

        public ICommand IgnoreCommand { get; }
        public ICommand DeletePostCommand { get; }
        public ICommand Suspend7DaysCommand { get; }
        public ICommand DeleteUserCommand { get; }
        public ICommand DetailsCommand { get; }

        public AdminReportsPage()
        {
            InitializeComponent();
            BindingContext = this;

            IgnoreCommand = new Command<ReportItem>(async (r) => await HandleDecisionAsync(r, "ignored", "none"));

            DeletePostCommand = new Command<ReportItem>(async (r) => await HandleDecisionAsync(r, "accepted", "delete_post"));

            Suspend7DaysCommand = new Command<ReportItem>(async (r) => await HandleDecisionAsync(r, "accepted", "suspend_user"));

            DeleteUserCommand = new Command<ReportItem>(async (r) => await HandleDecisionAsync(r, "accepted", "delete_user"));

            DetailsCommand = new Command<ReportItem>(async (r) => await ShowDetailsAsync(r));
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
                StatusText = "Carregando denúncias...";
                OnPropertyChanged(nameof(StatusText));

                Items.Clear();

                var list = await FirebaseDatabaseService.Instance.GetAllReportsAsync(400);

                // Exibe "open" primeiro
                var ordered = list
                    .OrderBy(r => (r.Status ?? "open") == "open" ? 0 : 1)
                    .ThenByDescending(r => r.CreatedAtUtcMs)
                    .ToList();

                foreach (var it in ordered)
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

        private async Task ShowDetailsAsync(ReportItem r)
        {
            if (r == null) return;

            var msg =
                $"ID: {r.Id}\n" +
                $"Tipo: {r.Type}\n" +
                $"Status: {r.Status}\n" +
                $"Motivo: {r.Reason}\n\n" +
                $"Reporter: {r.ReporterUserId}\n" +
                $"Alvo: {r.TargetUserName} ({r.TargetUserId})\n\n" +
                $"PostId: {r.PostId}\n" +
                $"Texto: {r.PostTextPreview}\n\n" +
                $"Área: {r.AppArea}\n" +
                $"Criado (UTC ms): {r.CreatedAtUtcMs}\n" +
                $"Ação admin: {r.AdminAction}\n" +
                $"Nota admin: {r.AdminNote}\n";

            await DisplayAlert("Detalhes", msg, "OK");
        }

        private async Task HandleDecisionAsync(ReportItem r, string status, string action)
        {
            if (r == null) return;

            // validações mínimas
            if (action == "delete_post")
            {
                if (!string.Equals(r.Type, "post", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(r.PostId))
                {
                    await DisplayAlert("Sem Post", "Essa denúncia não contém PostId para excluir.", "OK");
                    return;
                }
            }

            if ((action == "suspend_user" || action == "delete_user") && string.IsNullOrWhiteSpace(r.TargetUserId))
            {
                await DisplayAlert("Sem usuário alvo", "Essa denúncia não tem TargetUserId.", "OK");
                return;
            }

            var note = await DisplayPromptAsync(
                "Nota admin (opcional)",
                "Anote o motivo/ação:",
                "OK",
                "Cancelar",
                maxLength: 200);

            if (note == null) return;

            try
            {
                // 1) grava decisão no report
                await FirebaseDatabaseService.Instance.UpdateReportAdminDecisionAsync(r.Id, status, action, note);

                // 2) executa ação
                if (status == "accepted")
                {
                    if (action == "delete_post")
                    {
                        await FirebaseDatabaseService.Instance.DeletePostAsync(r.PostId);
                    }
                    else if (action == "suspend_user")
                    {
                        // 7 dias padrão (pedido)
                        await FirebaseDatabaseService.Instance.SuspendUserForDaysAsync(r.TargetUserId, 7, note);
                    }
                    else if (action == "delete_user")
                    {
                        await FirebaseDatabaseService.Instance.DeleteUserProfileAsync(r.TargetUserId);
                    }
                }

                await LoadAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", ex.Message, "OK");
            }
        }

        private async void OnBackTapped(object sender, EventArgs e) =>
            await Shell.Current.GoToAsync("..");

        private async void OnRefreshTapped(object sender, EventArgs e) =>
            await LoadAsync();
    }
}
