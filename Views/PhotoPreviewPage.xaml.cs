using System;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views
{
    public partial class PhotoPreviewPage : ContentPage
    {
        public PhotoPreviewPage(string imageUrl)
        {
            InitializeComponent();

            if (!string.IsNullOrWhiteSpace(imageUrl))
                PreviewImage.Source = imageUrl;
        }

        private async void OnCloseTapped(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        private async void OnBackgroundTapped(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}
