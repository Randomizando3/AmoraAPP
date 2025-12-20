using System;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views.Admin
{
    public partial class AdminHomePage : ContentPage
    {
        public AdminHomePage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await AdminUi.GuardAsync();
        }

        private async void OnBackTapped(object sender, EventArgs e) =>
            await Shell.Current.GoToAsync("..");

        private async void OnReportsClicked(object sender, EventArgs e) =>
            await Shell.Current.GoToAsync(nameof(AdminReportsPage));

        private async void OnPostsClicked(object sender, EventArgs e) =>
            await Shell.Current.GoToAsync(nameof(AdminPostsPage));

        private async void OnUsersClicked(object sender, EventArgs e) =>
            await Shell.Current.GoToAsync(nameof(AdminUsersPage));

        private async void OnVerificationsClicked(object sender, EventArgs e) =>
            await Shell.Current.GoToAsync(nameof(AdminVerificationsPage));

        private async void OnTicketsClicked(object sender, EventArgs e) =>
            await Shell.Current.GoToAsync(nameof(AdminTicketsPage));
    }
}
