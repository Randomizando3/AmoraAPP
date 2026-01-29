// Models/UserDal.cs
namespace AmoraApp.Models
{
    /// <summary>
    /// Modelo simples para persistência local da sessão.
    /// Guarde o mínimo possível aqui para evitar “vazar” contexto de admin.
    /// </summary>
    public class UserDal
    {
        public string Uid { get; set; } = string.Empty;

        /// <summary>
        /// Cache local do status admin (opcional).
        /// Se você já resolveu do jeito “sem cache” no bootstrap, pode ignorar.
        /// </summary>
        public bool IsAdmin { get; set; }

        public long SavedAtUtcMs { get; set; }
    }
}
