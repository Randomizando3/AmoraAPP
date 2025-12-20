using System;
using AmoraApp.Models;
using AmoraApp.Services;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views
{
    public partial class SupportPage : ContentPage
    {
        public SupportPage()
        {
            InitializeComponent();
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnSendClicked(object sender, EventArgs e)
        {
            var name = NameEntry.Text?.Trim() ?? "";
            var email = EmailEntry.Text?.Trim() ?? "";
            var phone = PhoneEntry.Text?.Trim() ?? "";
            var msg = MessageEditor.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(msg))
            {
                await DisplayAlert("Campos obrigatórios", "Informe nome, e-mail e mensagem.", "OK");
                return;
            }

            try
            {
                SendBtn.IsEnabled = false;

                var uid = FirebaseAuthService.Instance.CurrentUserUid ?? "";

                var ticket = new SupportTicket
                {
                    UserId = uid,
                    Name = name,
                    Email = email,
                    Phone = phone,
                    Message = msg,
                    Status = "open"
                };

                await FirebaseDatabaseService.Instance.CreateSupportTicketAsync(ticket);

                await DisplayAlert("Enviado", "Sua mensagem foi enviada ao suporte.", "OK");
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", "Falha ao enviar: " + ex.Message, "OK");
            }
            finally
            {
                SendBtn.IsEnabled = true;
            }
        }
    }
}
