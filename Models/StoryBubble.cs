namespace AmoraApp.Models
{
    public class StoryBubble
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;

        // Foto de perfil do usuário
        public string PhotoUrl { get; set; } = string.Empty;

        // Última imagem de story (para o preview da bolha)
        public string PreviewImageUrl { get; set; } = string.Empty;

        // Indica se esta bolha representa o próprio usuário
        public bool IsMe { get; set; }

        // Indica se o usuário é um match seu (usado para mostrar o coraçãozinho)
        public bool IsMatch { get; set; }


        public bool HasStory => !string.IsNullOrWhiteSpace(PreviewImageUrl);

        // O que será exibido no círculo do feed
        public string DisplayImageUrl =>
            !string.IsNullOrWhiteSpace(PreviewImageUrl)
                ? PreviewImageUrl
                : PhotoUrl;
    }
}


