using AmoraApp.Services;
using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace AmoraApp.Views
{
    public partial class UpgradePage : ContentPage
    {
        private string CurrentUserId =>
            FirebaseAuthService.Instance.CurrentUserUid ?? string.Empty;

        public UpgradePage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            await LoadPlanAsync();
            await LoadBoostsAsync();
        }

        private async Task LoadPlanAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentUserId))
                {
                    CurrentPlanLabel.Text = "Faça login para ver seu plano.";
                    return;
                }

                var plan = await PlanService.Instance.GetUserPlanAsync(CurrentUserId);
                var planName = PlanService.Instance.GetPlanDisplayName(plan);

                CurrentPlanLabel.Text = $"Plano atual: {planName}";

                // Deixa o botão do plano atual desabilitado / marcado
                FreePlanButton.IsEnabled = plan != PlanType.Free;
                PlusPlanButton.IsEnabled = plan != PlanType.Plus;
                PremiumPlanButton.IsEnabled = plan != PlanType.Premium;

                if (plan == PlanType.Free)
                {
                    FreePlanButton.Text = "Seu plano atual";
                    PlusPlanButton.Text = "Assinar Plus";
                    PremiumPlanButton.Text = "Assinar Premium";
                }
                else if (plan == PlanType.Plus)
                {
                    FreePlanButton.Text = "Mudar para Grátis";
                    PlusPlanButton.Text = "Seu plano atual";
                    PremiumPlanButton.Text = "Assinar Premium";
                }
                else if (plan == PlanType.Premium)
                {
                    FreePlanButton.Text = "Mudar para Grátis";
                    PlusPlanButton.Text = "Mudar para Plus";
                    PremiumPlanButton.Text = "Seu plano atual";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UpgradePage] Erro ao carregar plano: {ex}");
                CurrentPlanLabel.Text = "Não foi possível carregar seu plano.";
            }
        }

        private async Task LoadBoostsAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentUserId))
                {
                    BoostsInfoLabel.Text = "Faça login para ver seus boosts.";
                    return;
                }

                // Por enquanto, apenas usa o service como stub.
                var boosts = await PlanService.Instance.GetUserBoostsAsync(CurrentUserId);
                BoostsInfoLabel.Text = $"Você tem {boosts} boost(s) disponível(is).";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UpgradePage] Erro ao carregar boosts: {ex}");
                BoostsInfoLabel.Text = "Não foi possível carregar seus boosts.";
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            try
            {
                await Navigation.PopAsync();
            }
            catch { }
        }

        private async void OnPlusPlanClicked(object sender, EventArgs e)
        {
            await HandleUpgradeAsync(PlanType.Plus);
        }

        private async void OnPremiumPlanClicked(object sender, EventArgs e)
        {
            await HandleUpgradeAsync(PlanType.Premium);
        }

        private async Task HandleUpgradeAsync(PlanType targetPlan)
        {
            if (string.IsNullOrWhiteSpace(CurrentUserId))
            {
                await DisplayAlert("Login necessário",
                    "Entre na sua conta para assinar um plano.",
                    "OK");
                return;
            }

            // Aqui no futuro você coloca cobrança real (Google Play / Apple / Mercado Pago / etc.)
            var name = PlanService.Instance.GetPlanDisplayName(targetPlan);

            var confirm = await DisplayAlert(
                "Continuar para pagamento",
                $"Você será direcionado para a tela de pagamento do plano {name}.",
                "Continuar",
                "Cancelar");

            if (!confirm)
                return;

            await DisplayAlert(
                "Simulação",
                "Integração de pagamento ainda não implementada. Aqui você chamaria o fluxo de cobrança.",
                "OK");

            // Se quiser simular upgrade:
            // await PlanService.Instance.SetUserPlanAsync(CurrentUserId, targetPlan);
            // await LoadPlanAsync();
        }

        private async void OnBuy3BoostsClicked(object sender, EventArgs e)
        {
            await HandleBuyBoostsAsync(3);
        }

        private async void OnBuy10BoostsClicked(object sender, EventArgs e)
        {
            await HandleBuyBoostsAsync(10);
        }

        private async Task HandleBuyBoostsAsync(int quantity)
        {
            if (string.IsNullOrWhiteSpace(CurrentUserId))
            {
                await DisplayAlert("Login necessário",
                    "Entre na sua conta para comprar boosts.",
                    "OK");
                return;
            }

            var confirm = await DisplayAlert(
                "Comprar boosts",
                $"Você será direcionado para o pagamento de {quantity} boost(s).",
                "Continuar",
                "Cancelar");

            if (!confirm)
                return;

            await DisplayAlert(
                "Simulação",
                "Integração de pagamento ainda não está ativa. Aqui entraria o fluxo real de cobrança.",
                "OK");

            // Para simular que comprou, se quiser:
            // await PlanService.Instance.AddUserBoostsAsync(CurrentUserId, quantity);
            // await LoadBoostsAsync();
        }
    }
}
