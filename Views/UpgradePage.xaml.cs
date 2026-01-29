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

        // Exibição (você informou estes valores)
        private const string PlusMonthlyPriceText = "R$ 24,90/mês";
        private const string PlusYearlyPriceText = "R$ 298,80/ano";

        private const string PremiumMonthlyPriceText = "R$ 49,90/mês";
        private const string PremiumYearlyPriceText = "R$ 598,80/ano";

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

#if ANDROID
            // Restaura compras (subs ativas + inapps pendentes) para evitar “paguei e não ativou”
            if (!string.IsNullOrWhiteSpace(CurrentUserId))
            {
                try { await GooglePlayBillingService.Instance.RestorePurchasesAsync(CurrentUserId); }
                catch { /* best-effort */ }

                // Recarrega após restore
                await LoadPlanAsync();
                await LoadBoostsAsync();
            }
#endif
        }

        // =========================
        // CUPOM
        // =========================
        private async void OnApplyCouponClicked(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentUserId))
                {
                    await DisplayAlert("Login necessário",
                        "Entre na sua conta para ativar um cupom.",
                        "OK");
                    return;
                }

                var code = CouponEntry?.Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(code))
                {
                    await DisplayAlert("Cupom",
                        "Digite um cupom para ativar.",
                        "OK");
                    return;
                }

                ApplyCouponButton.IsEnabled = false;

                var result = await PlanService.Instance.RedeemCouponAsync(CurrentUserId, code);

                if (!result.Success)
                {
                    await DisplayAlert("Cupom", result.Message, "OK");
                    return;
                }

                CouponEntry.Text = "";
                await DisplayAlert("Cupom", result.Message, "OK");

                await LoadPlanAsync();
                await LoadBoostsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UpgradePage] Erro ao aplicar cupom: {ex}");
                await DisplayAlert("Cupom", "Não foi possível ativar o cupom agora.", "OK");
            }
            finally
            {
                ApplyCouponButton.IsEnabled = true;
            }
        }

        private async Task LoadPlanAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentUserId))
                {
                    CurrentPlanLabel.Text = "Faça login para ver seu plano.";
                    RemainingDaysLabel.IsVisible = false;
                    return;
                }

                var info = await PlanService.Instance.GetUserPlanStatusAsync(CurrentUserId);

                var plan = info.Plan;
                var planName = PlanService.Instance.GetPlanDisplayName(plan);

                CurrentPlanLabel.Text = $"Seu plano atual é: {planName}";

                if (plan != PlanType.Free && info.RemainingDays > 0)
                {
                    RemainingDaysLabel.Text = $"Restam {info.RemainingDays} dia(s)";
                    RemainingDaysLabel.IsVisible = true;
                }
                else
                {
                    RemainingDaysLabel.IsVisible = false;
                }

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
                RemainingDaysLabel.IsVisible = false;
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

        private async void OnPlusPlanClicked(object sender, EventArgs e)
        {
            await HandleUpgradeAsync(PlanType.Plus);
        }

        private async void OnPremiumPlanClicked(object sender, EventArgs e)
        {
            await HandleUpgradeAsync(PlanType.Premium);
        }

        private string GetSubscriptionProductId(PlanType targetPlan, PlanPeriod period)
        {
            if (targetPlan == PlanType.Plus)
                return period == PlanPeriod.Yearly ? "plus_yearly" : "plus_monthly";

            if (targetPlan == PlanType.Premium)
                return period == PlanPeriod.Yearly ? "premium_yearly" : "premium_monthly";

            return "";
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

            var name = PlanService.Instance.GetPlanDisplayName(targetPlan);
            var periodText = (_selectedPeriod == PlanPeriod.Monthly ? "mensal" : "anual");

            var confirm = await DisplayAlert(
                "Confirmar assinatura",
                $"Você vai assinar o plano {name} ({periodText}) pelo Google Play.",
                "Continuar",
                "Cancelar");

            if (!confirm)
                return;

#if ANDROID
            try
            {
                SetBusy(true);

                var productId = GetSubscriptionProductId(targetPlan, _selectedPeriod);

                // A concessão do benefício é feita no callback do Billing (ack + grant),
                // mas mantemos aqui a intenção para clareza.
                var result = await GooglePlayBillingService.Instance.BuySubscriptionAsync(
                    CurrentUserId,
                    productId,
                    async (_) =>
                    {
                        await PlanService.Instance.ActivatePlanAsync(CurrentUserId, targetPlan, _selectedPeriod);
                    });

                if (!result.ok)
                {
                    await DisplayAlert("Pagamento", result.message, "OK");
                    return;
                }

                await DisplayAlert("Pagamento", "Assinatura concluída. Seu plano foi atualizado.", "OK");

                await LoadPlanAsync();
                await LoadBoostsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UpgradePage] Erro billing: {ex}");
                await DisplayAlert("Pagamento", "Não foi possível concluir o pagamento agora.", "OK");
            }
            finally
            {
                SetBusy(false);
            }
#else
            await DisplayAlert("Indisponível", "Assinaturas estão disponíveis apenas no Android (Google Play).", "OK");
#endif
        }

        private async void OnBuy3BoostsClicked(object sender, EventArgs e)
        {
            await HandleBuyBoostsAsync(3);
        }

        private async void OnBuy10BoostsClicked(object sender, EventArgs e)
        {
            await HandleBuyBoostsAsync(10);
        }

        private string GetBoostProductId(int quantity)
            => quantity == 10 ? "boost_10" : "boost_3";

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
                $"Você vai comprar {quantity} boost(s) pelo Google Play.",
                "Continuar",
                "Cancelar");

            if (!confirm)
                return;

#if ANDROID
            try
            {
                SetBusy(true);

                var productId = GetBoostProductId(quantity);

                var result = await GooglePlayBillingService.Instance.BuyInAppAsync(
                    CurrentUserId,
                    productId,
                    async (_) =>
                    {
                        await PlanService.Instance.AddUserBoostsAsync(CurrentUserId, quantity);
                    });

                if (!result.ok)
                {
                    await DisplayAlert("Pagamento", result.message, "OK");
                    return;
                }

                await DisplayAlert("Pagamento", "Compra concluída. Boosts adicionados à sua conta.", "OK");
                await LoadBoostsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UpgradePage] Erro billing boosts: {ex}");
                await DisplayAlert("Pagamento", "Não foi possível concluir o pagamento agora.", "OK");
            }
            finally
            {
                SetBusy(false);
            }
#else
            await DisplayAlert("Indisponível", "Boosts por Google Play estão disponíveis apenas no Android.", "OK");
#endif
        }

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

        private void SetBusy(bool busy)
        {
            PlusPlanButton.IsEnabled = !busy;
            PremiumPlanButton.IsEnabled = !busy;
            ApplyCouponButton.IsEnabled = !busy;

            // Botões boosts (não têm x:Name, então deixamos assim por simplicidade)
            // Se quiser, eu te devolvo o XAML com x:Name para desabilitar também.
        }
    }
}
