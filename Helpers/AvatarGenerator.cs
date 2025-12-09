using System;
using Microsoft.Maui.Controls;

namespace AmoraApp.Helpers
{
    /// <summary>
    /// Gera e resolve avatares automáticos de grupo usando arquivos:
    /// group_placeholder.png
    /// group_placeholder1.png
    /// ...
    /// group_placeholder10.png
    ///
    /// Também entende o formato antigo "autoAvatar://..." (compatibilidade).
    /// </summary>
    public static class AvatarGenerator
    {
        private const string OldAutoPrefix = "autoAvatar://";
        // Índices possíveis: 0..10  =>  group_placeholder(.png) + group_placeholder1..10.png
        private const int MaxVariants = 11;

        /// <summary>
        /// Verifica se a url representa um avatar automático de grupo.
        /// Aceita:
        /// - Novo: "group_placeholder*.png"
        /// - Antigo: "autoAvatar://..."
        /// </summary>
        public static bool IsAutoGroupAvatar(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return url.StartsWith("group_placeholder", StringComparison.OrdinalIgnoreCase)
                   || url.StartsWith(OldAutoPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gera um avatar automático de grupo, baseado em um "hash" estável:
        /// - Usa (seed + chatId) para definir o índice 0..10
        /// - Assim, grupos diferentes tendem a ter ícones diferentes,
        ///   e o mesmo grupo sempre fica com o mesmo placeholder.
        ///
        /// Retorno possível:
        /// - "group_placeholder.png"       (índice 0)
        /// - "group_placeholder1.png" ... "group_placeholder10.png"
        /// </summary>
        public static string GetGroupAvatarUrl(string seed = "", string? chatId = null)
        {
            // Monta uma chave base para o hash
            var key = $"{seed}|{chatId}";
            if (string.IsNullOrWhiteSpace(key))
                key = Guid.NewGuid().ToString(); // fallback (quase nunca cai aqui)

            int hash = key.GetHashCode();
            if (hash == int.MinValue)
                hash = int.MaxValue;

            int index = Math.Abs(hash) % MaxVariants; // 0..10

            if (index == 0)
                return "group_placeholder.png";

            return $"group_placeholder{index}.png";
        }

        /// <summary>
        /// Converte uma URL de avatar automático em ImageSource.
        /// Aceita:
        /// - "group_placeholder.png" / "group_placeholderX.png"
        /// - "autoAvatar://group-X" (formato antigo)
        /// </summary>
        public static ImageSource GetGroupAvatarImageSourceFromUrl(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                    return ImageSource.FromFile("group_placeholder.png");

                // Compat com formato antigo "autoAvatar://group-X"
                if (url.StartsWith(OldAutoPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var inner = url.Replace(OldAutoPrefix, "", StringComparison.OrdinalIgnoreCase);
                    // esperado: "group-X"
                    var parts = inner.Split('-', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && int.TryParse(parts[1], out int index))
                    {
                        if (index < 0 || index >= MaxVariants)
                            index = 0;

                        var fileName = index == 0
                            ? "group_placeholder.png"
                            : $"group_placeholder{index}.png";

                        return ImageSource.FromFile(fileName);
                    }

                    return ImageSource.FromFile("group_placeholder.png");
                }

                // Formato novo: já é nome de arquivo
                if (url.StartsWith("group_placeholder", StringComparison.OrdinalIgnoreCase))
                {
                    return ImageSource.FromFile(url);
                }

                // Qualquer outra coisa: tenta usar direto (URL de foto custom etc.),
                // o converter AvatarSourceConverter cuida disso também.
                return ImageSource.FromFile(url);
            }
            catch
            {
                return ImageSource.FromFile("group_placeholder.png");
            }
        }
    }
}
