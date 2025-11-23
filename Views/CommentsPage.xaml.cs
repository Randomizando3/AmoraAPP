using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AmoraApp.Models;
using AmoraApp.Services;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views
{
    public partial class CommentsPage : ContentPage
    {
        private readonly Post _post;
        public ObservableCollection<Comment> Comments { get; } = new();

        public CommentsPage(Post post)
        {
            InitializeComponent();
            _post = post;
            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadCommentsAsync();
        }

        private async Task LoadCommentsAsync()
        {
            var list = await FirebaseDatabaseService.Instance.GetCommentsAsync(_post.Id);

            Comments.Clear();
            foreach (var c in list)
                Comments.Add(c);
        }

        private async void OnSendClicked(object sender, System.EventArgs e)
        {
            var text = CommentEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;

            var user = FirebaseAuthService.Instance.GetCurrentUser();
            if (user == null)
            {
                await DisplayAlert("Erro", "Usuário não logado.", "OK");
                return;
            }

            var profile = await FirebaseDatabaseService.Instance.GetUserProfileAsync(user.Uid);
            var name = profile?.DisplayName ?? user.Info.DisplayName ?? user.Info.Email;

            var comment = new Comment
            {
                PostId = _post.Id,
                UserId = user.Uid,
                UserName = name,
                Text = text
            };

            await FirebaseDatabaseService.Instance.AddCommentAsync(comment);

            CommentEntry.Text = string.Empty;
            await LoadCommentsAsync();
        }

        private async void OnCloseTapped(object sender, System.EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}
