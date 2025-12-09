using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace AmoraApp.Helpers
{
    /// <summary>
    /// Converter usado na ChatListPage para transformar PhotoUrl em ImageSource.
    /// Aceita:
    /// - string vazia/null -> user_placeholder.png
    /// - autoAvatar://...  -> avatar automático de grupo (AvatarGenerator)
    /// - URL HTTP/HTTPS    -> ImageSource.FromUri
    /// - Nome de arquivo   -> ImageSource.FromFile
    /// </summary>
    public class AvatarSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var url = value as string;

            // Avatar automático de grupo (autoAvatar://...)
            if (AvatarGenerator.IsAutoGroupAvatar(url))
            {
                return AvatarGenerator.GetGroupAvatarImageSourceFromUrl(url!);
            }

            // Sem URL -> placeholder padrão
            if (string.IsNullOrWhiteSpace(url))
            {
                return ImageSource.FromFile("user_placeholder.png");
            }

            // Tenta como URL absoluta
            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    return ImageSource.FromUri(uri);
                }

                // Se não for URL absoluta, tenta como arquivo local (ex: "foto.png")
                return ImageSource.FromFile(url);
            }
            catch
            {
                // Em qualquer problema, usa placeholder
                return ImageSource.FromFile("user_placeholder.png");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
