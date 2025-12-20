using Microsoft.Maui.Controls;
using AmoraApp.Views;
using AmoraApp.Views.Admin;

namespace AmoraApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Rotas gerais
            Routing.RegisterRoute(nameof(VerificationPage), typeof(VerificationPage));
            Routing.RegisterRoute(nameof(SupportPage), typeof(SupportPage));

            // Rotas Admin
            Routing.RegisterRoute(nameof(AdminHomePage), typeof(AdminHomePage));
            Routing.RegisterRoute(nameof(AdminReportsPage), typeof(AdminReportsPage));
            Routing.RegisterRoute(nameof(AdminPostsPage), typeof(AdminPostsPage));
            Routing.RegisterRoute(nameof(AdminUsersPage), typeof(AdminUsersPage));
            Routing.RegisterRoute(nameof(AdminVerificationsPage), typeof(AdminVerificationsPage));
            Routing.RegisterRoute(nameof(AdminTicketsPage), typeof(AdminTicketsPage));
        }
    }
}
