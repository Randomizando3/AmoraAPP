using AmoraApp.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Threading.Tasks;

namespace AmoraApp.Views
{
    public partial class UpgradePage : ContentPage
    {
        private string CurrentUserId =>
            FirebaseAuthService.Instance.CurrentUserUid ?? string.Empty;

        // Período selecionado na UI (padrão: mensal)
        private PlanPeriod _selectedPeriod = PlanPeriod.Monthly;

        // Valores fictícios para exibição
        private const string PlusMonthlyPriceText = "R$ 29,90/mês";
        private const string PlusYearlyPriceText = "R$ 299,00/ano";

        private const string PremiumMonthlyPriceText = "R$ 49,90/mês";
        private const string PremiumYearlyPriceText = "R$ 499,00/ano";

        public UpgradePage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            UpdatePeriodUI();
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

                // Trecho explícito: "Seu plano atual é tal..."
                CurrentPlanLabel.Text = $"Seu plano atual é: {planName}";

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

        /// <summary>
        /// Lida com o fluxo de upgrade.
        /// Na simulação: depois do OK no alerta, já troca o plano e atualiza UI.
        /// </summary>
        private async Task HandleUpgradeAsync(PlanType targetPlan)
        {
            if (string.IsNullOrWhiteSpace(CurrentUserId))
            {
                await DisplayAlert("Login necessário",
                    "Entre na sua conta para assinar um plano.",
                    "OK");
                return;
            }

            var name = PlanService.Instance.GetPlanDisplayName(targetPlan);

            var confirm = await DisplayAlert(
                "Continuar para pagamento",
                $"Você será direcionado para a tela de pagamento do plano {name} ({(_selectedPeriod == PlanPeriod.Monthly ? "mensal" : "anual")}).",
                "Continuar",
                "Cancelar");

            if (!confirm)
                return;

            // SIMULAÇÃO – aqui ainda não tem gateway de pagamento
            await DisplayAlert(
                "Simulação",
                "Integração de pagamento ainda não implementada. Nesta simulação, o plano será ativado agora.",
                "OK");

            // >>> AQUI já ativamos o plano de verdade na simulação <<<
            await PlanService.Instance.ActivatePlanAsync(CurrentUserId, targetPlan, _selectedPeriod);

            // Atualiza UI (badge + botões + boosts incluídos)
            await LoadPlanAsync();
            await LoadBoostsAsync();
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

            // Simulação de compra
            await DisplayAlert(
                "Simulação",
                "Integração de pagamento ainda não está ativa. Nesta simulação, os boosts serão adicionados agora.",
                "OK");

            // Adiciona os boosts fictícios imediatamente
            await PlanService.Instance.AddUserBoostsAsync(CurrentUserId, quantity);
            await LoadBoostsAsync();
        }

        // =========================================================
        // UI – Alternar Mensal / Anual
        // =========================================================

        private void OnMonthlyTapped(object sender, EventArgs e)
        {
            _selectedPeriod = PlanPeriod.Monthly;
            UpdatePeriodUI();
        }

        private void OnYearlyTapped(object sender, EventArgs e)
        {
            _selectedPeriod = PlanPeriod.Yearly;
            UpdatePeriodUI();
        }

        private void UpdatePeriodUI()
        {
            if (_selectedPeriod == PlanPeriod.Monthly)
            {
                MonthlyChip.BackgroundColor = Color.FromArgb("#5d259c");
                MonthlyChipLabel.TextColor = Colors.White;

                YearlyChip.BackgroundColor = Colors.Transparent;
                YearlyChip.BorderColor = Color.FromArgb("#5d259c");
                YearlyChipLabel.TextColor = Color.FromArgb("#5d259c");

                PlusPriceLabel.Text = PlusMonthlyPriceText;
                PremiumPriceLabel.Text = PremiumMonthlyPriceText;
            }
            else
            {
                YearlyChip.BackgroundColor = Color.FromArgb("#5d259c");
                YearlyChipLabel.TextColor = Colors.White;

                MonthlyChip.BackgroundColor = Colors.Transparent;
                MonthlyChipLabel.TextColor = Color.FromArgb("#5d259c");
                MonthlyChip.BorderColor = Color.FromArgb("#5d259c");

                PlusPriceLabel.Text = PlusYearlyPriceText;
                PremiumPriceLabel.Text = PremiumYearlyPriceText;
            }
        }
    }
}
