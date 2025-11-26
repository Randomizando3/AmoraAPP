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
            if (list == null) return;

            foreach (var c in list)
                Comments.Add(c);
        }

        private async void OnSendClicked(object sender, System.EventArgs e)
        {
            var text = CommentEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;

            var auth = FirebaseAuthService.Instance;

            // tenta pegar UID de forma resiliente
            var uid = auth.CurrentUserUid;
            var user = auth.GetCurrentUser();
            if (string.IsNullOrEmpty(uid) && user != null)
                uid = user.Uid;

            if (string.IsNullOrEmpty(uid))
            {
                await DisplayAlert("Erro", "Usuário não logado.", "OK");
                return;
            }

            var profile = await FirebaseDatabaseService.Instance.GetUserProfileAsync(uid);

            var name =
                profile?.DisplayName ??
                user?.Info?.DisplayName ??
                user?.Info?.Email ??
                "Usuário";

            var comment = new Comment
            {
                PostId = _post.Id,
                UserId = uid,
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
