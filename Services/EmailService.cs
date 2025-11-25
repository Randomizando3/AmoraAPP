using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace AmoraApp.Services
{
    public class EmailService
    {
        public static EmailService Instance { get; } = new EmailService();

        private EmailService() { }

        /// <summary>
        /// Envia um email com o código de verificação usando SMTP Titan.
        /// </summary>
        public async Task SendVerificationCodeAsync(string toEmail, string code)
        {
            var fromEmail = "software@showdeimagem.com.br";
            var fromPassword = "software_Show2024";

            using var message = new MailMessage();
            message.From = new MailAddress(fromEmail, "AmoraApp");
            message.To.Add(toEmail);
            message.Subject = "Código de confirmação - AmoraApp";
            message.Body =
                $"Olá!\n\n" +
                $"Seu código de confirmação é: {code}\n\n" +
                $"Se você não solicitou este cadastro, ignore este email.\n\n" +
                $"Equipe AmoraApp.";
            message.IsBodyHtml = false;

            // Titan SMTP
            // Host: smtp.titan.email
            // Porta: 587 (STARTTLS)
            using var client = new SmtpClient("smtp.titan.email", 587)
            {
                EnableSsl = true,                 // STARTTLS
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromEmail, fromPassword),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000                   // 15s de timeout pra não travar
            };

            await client.SendMailAsync(message);
        }
    }
}
