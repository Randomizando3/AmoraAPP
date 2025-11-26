using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace AmoraApp.Models
{
    public class Post : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _userId = string.Empty;
        private string _userName = string.Empty;
        private string _userPhotoUrl = string.Empty;
        private string _text = string.Empty;
        private string _imageUrl = string.Empty;
        private DateTime _createdAt = DateTime.UtcNow;
        private int _likes;
        private int _commentsCount;
        private bool _likedByMe;
        private List<Comment> _recentComments = new();

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public string Id
        {
            get => _id;
            set { if (_id != value) { _id = value; OnPropertyChanged(); } }
        }

        public string UserId
        {
            get => _userId;
            set { if (_userId != value) { _userId = value; OnPropertyChanged(); } }
        }

        public string UserName
        {
            get => _userName;
            set { if (_userName != value) { _userName = value; OnPropertyChanged(); } }
        }

        public string UserPhotoUrl
        {
            get => _userPhotoUrl;
            set { if (_userPhotoUrl != value) { _userPhotoUrl = value; OnPropertyChanged(); } }
        }

        public string Text
        {
            get => _text;
            set { if (_text != value) { _text = value; OnPropertyChanged(); } }
        }

        public string ImageUrl
        {
            get => _imageUrl;
            set { if (_imageUrl != value) { _imageUrl = value; OnPropertyChanged(); } }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set { if (_createdAt != value) { _createdAt = value; OnPropertyChanged(); } }
        }

        public int Likes
        {
            get => _likes;
            set { if (_likes != value) { _likes = value; OnPropertyChanged(); } }
        }

        public int CommentsCount
        {
            get => _commentsCount;
            set { if (_commentsCount != value) { _commentsCount = value; OnPropertyChanged(); } }
        }

        // Se o usuário atual já deu like neste post
        [JsonIgnore]
        public bool LikedByMe
        {
            get => _likedByMe;
            set { if (_likedByMe != value) { _likedByMe = value; OnPropertyChanged(); } }
        }

        // 1 ou 2 comentários recentes para preview no feed
        [JsonIgnore]
        public List<Comment> RecentComments
        {
            get => _recentComments;
            set { if (_recentComments != value) { _recentComments = value; OnPropertyChanged(); } }
        }
    }
}
