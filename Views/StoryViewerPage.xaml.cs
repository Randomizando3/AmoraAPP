using AmoraApp.Models;
using AmoraApp.Services;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;


namespace AmoraApp.Views
{
    public partial class StoryViewerPage : ContentPage
    {
        public ObservableCollection<StoryItem> Stories { get; } = new();

        private readonly IList<StoryBubble> _bubbles;
        private int _currentBubbleIndex;

        private string _userId = string.Empty;
        private readonly string _meId;

        private bool _isTimerRunning;
        private const double StoryDurationSeconds = 5.0;

        private int _currentIndex = 0;

        private int _currentLikes = 0;
        private bool _likedByMe = false;

        // Construtor antigo continua existindo pra compatibilidade
        public StoryViewerPage(StoryBubble bubble)
            : this(new List<StoryBubble> { bubble }, 0)
        {
        }

        // Novo construtor: recebe a lista de bolhas + índice atual
        public StoryViewerPage(IList<StoryBubble> bubbles, int startIndex)
        {
            InitializeComponent();

            _bubbles = bubbles ?? new List<StoryBubble>();
            if (_bubbles.Count == 0)
            {
                // nada pra mostrar
                _currentBubbleIndex = 0;
            }
            else
            {
                _currentBubbleIndex = startIndex;
                if (_currentBubbleIndex < 0 || _currentBubbleIndex >= _bubbles.Count)
                    _currentBubbleIndex = 0;
            }

            _meId = FirebaseAuthService.Instance.CurrentUserUid ?? string.Empty;

            _ = LoadStoriesForCurrentBubbleAsync();
        }

        private async Task LoadStoriesForCurrentBubbleAsync()
        {
            if (_bubbles == null || _bubbles.Count == 0)
            {
                await Navigation.PopModalAsync();
                return;
            }

            var bubble = _bubbles[_currentBubbleIndex];
            _userId = bubble.UserId;

            UserNameLabel.Text = bubble.UserName;

            var list = await StoryService.Instance.GetStoriesAsync(_userId);

            Stories.Clear();
            foreach (var s in list)
                Stories.Add(s);

            if (Stories.Count > 0)
            {
                _currentIndex = 0;
                await ShowCurrentStoryAsync();
                StartProgress();
            }
            else
            {
                // Esse usuário não tem mais stories válidos → tenta ir pro próximo
                if (!await GoToNextUserWithStoriesAsync())
                {
                    await Navigation.PopModalAsync();
                }
            }
        }

        private async Task ShowCurrentStoryAsync()
        {
            if (_currentIndex < 0 || _currentIndex >= Stories.Count)
                return;

            var story = Stories[_currentIndex];

            // Atualiza imagem primeiro
            StoryImage.Source = story.ImageUrl;

            // Depois atualiza likes daquele story
            await LoadLikesForCurrentStoryAsync();
        }

        // -----------------------------
        // TIMER / PROGRESSO
        // -----------------------------

        private void StartProgress()
        {
            _isTimerRunning = false;
            ProgressBarFill.ScaleX = 0;

            if (Stories.Count == 0)
                return;

            _isTimerRunning = true;
            double elapsed = 0.0;

            Device.StartTimer(TimeSpan.FromMilliseconds(50), () =>
            {
                if (!_isTimerRunning)
                    return false;

                elapsed += 0.05; // 50ms = 0,05s
                var t = elapsed / StoryDurationSeconds;

                if (t >= 1.0)
                {
                    ProgressBarFill.ScaleX = 1;
                    _isTimerRunning = false;

                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await NextStoryAsync();
                    });

                    return false;
                }

                ProgressBarFill.ScaleX = t;
                return true;
            });
        }

        private async Task NextStoryAsync()
        {
            if (Stories.Count == 0)
                return;

            if (_currentIndex < Stories.Count - 1)
            {
                _currentIndex++;
                await ShowCurrentStoryAsync();
                StartProgress();
            }
            else
            {
                // Acabaram os stories deste usuário → tenta ir pro próximo usuário com stories
                if (!await GoToNextUserWithStoriesAsync())
                {
                    // Não tem mais ninguém com story → fecha viewer
                    await Navigation.PopModalAsync();
                }
            }
        }

        private async Task PrevStoryAsync()
        {
            if (Stories.Count == 0)
                return;

            if (_currentIndex > 0)
            {
                _currentIndex--;
                await ShowCurrentStoryAsync();
                StartProgress();
            }
            else
            {
                // No primeiro story deste usuário → por enquanto só fecha
                await Navigation.PopModalAsync();
            }
        }

        /// <summary>
        /// Procura o PRÓXIMO usuário na lista que tenha stories válidos.
        /// Não dá loop infinito – se não encontrar depois do atual, retorna false.
        /// </summary>
        private async Task<bool> GoToNextUserWithStoriesAsync()
        {
            if (_bubbles == null || _bubbles.Count == 0)
                return false;

            var currentUserId = _userId;

            for (int i = _currentBubbleIndex + 1; i < _bubbles.Count; i++)
            {
                var bubble = _bubbles[i];

                // Só considera quem tem preview (HasStory = true)
                if (!bubble.HasStory)
                    continue;

                var list = await StoryService.Instance.GetStoriesAsync(bubble.UserId);
                if (list != null && list.Count > 0)
                {
                    _currentBubbleIndex = i;
                    _userId = bubble.UserId;
                    UserNameLabel.Text = bubble.UserName;

                    Stories.Clear();
                    foreach (var s in list)
                        Stories.Add(s);

                    _currentIndex = 0;
                    await ShowCurrentStoryAsync();
                    StartProgress();
                    return true;
                }
            }

            // Não achou nenhum depois do atual
            return false;
        }

        // -----------------------------
        // TAP ZONES
        // -----------------------------

        private async void OnNextAreaTapped(object sender, EventArgs e)
        {
            _isTimerRunning = false;
            await NextStoryAsync();
        }

        private async void OnPrevAreaTapped(object sender, EventArgs e)
        {
            _isTimerRunning = false;
            await PrevStoryAsync();
        }

        private async void OnCloseTapped(object sender, EventArgs e)
        {
            _isTimerRunning = false;
            await Navigation.PopModalAsync();
        }

        // -----------------------------
        // LIKES (toggle por usuário)
        // -----------------------------

        private async Task LoadLikesForCurrentStoryAsync()
        {
            if (_currentIndex < 0 || _currentIndex >= Stories.Count)
            {
                _currentLikes = 0;
                _likedByMe = false;
                LikesLabel.Text = "0";
                return;
            }

            var story = Stories[_currentIndex];

            var (likes, likedByMe) = await StoryService.Instance.GetStoryLikesAsync(
                _userId,
                story.Id,
                _meId);

            _currentLikes = likes;
            _likedByMe = likedByMe;

            LikesLabel.Text = _currentLikes.ToString();
        }

        private async void OnLikeTapped(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_meId))
            {
                await DisplayAlert("Ops", "Você precisa estar logado para curtir stories.", "OK");
                return;
            }

            if (_currentIndex < 0 || _currentIndex >= Stories.Count)
                return;

            var story = Stories[_currentIndex];

            await StoryService.Instance.ToggleLikeAsync(_userId, story.Id, _meId);
            await LoadLikesForCurrentStoryAsync();
        }
    }
}
