using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using AmoraApp.Models;
using AmoraApp.Services;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views.Admin
{
    public partial class AdminPostsPage : ContentPage
    {
        public ObservableCollection<Post> Items { get; } = new();
        public string StatusText { get; set; } = "Carregando...";

        public ICommand DeleteCommand { get; }

        public AdminPostsPage()
        {
            InitializeComponent();
            BindingContext = this;

            DeleteCommand = new Command<Post>(async (p) => await DeleteAsync(p));
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
                StatusText = "Carregando posts...";
                OnPropertyChanged(nameof(StatusText));

                Items.Clear();
                var list = await FirebaseDatabaseService.Instance.GetAllPostsAsync(400);
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

        private async Task DeleteAsync(Post p)
        {
            if (p == null) return;

            var ok = await DisplayAlert("Deletar post", "Confirma deletar este post?", "Deletar", "Cancelar");
            if (!ok) return;

            try
            {
                await FirebaseDatabaseService.Instance.DeletePostAsync(p.Id);
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
