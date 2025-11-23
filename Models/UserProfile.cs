using System.Collections.Generic;

namespace AmoraApp.Models
{
    public class UserProfile
    {
        public string Id { get; set; } = string.Empty; // uid Firebase
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public string Bio { get; set; } = string.Empty;

        // Cargo / profissão
        public string JobTitle { get; set; } = string.Empty;

        // Escolaridade (nível) – ex.: "Superior completo"
        public string EducationLevel { get; set; } = string.Empty;

        // Escola / faculdade
        public string EducationInstitution { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;

        // Campo tradicional de idade (mantido pra filtros / compatibilidade)
        public int Age { get; set; } = 18;

        // Data de nascimento em Unix ms (UTC)
        public long BirthDateUtc { get; set; } = 0;

        // Gênero (inclui opção "Prefiro não dizer")
        public string Gender { get; set; } = "Not set";

        // Orientação sexual (inclui "Prefiro não dizer")
        public string SexualOrientation { get; set; } = string.Empty;

        // Religião (campo extra)
        public string Religion { get; set; } = string.Empty;

        // Foto principal (usada em Discover, bolhas de story, etc.)
        public string PhotoUrl { get; set; } = string.Empty;

        // LEGADO: sua galeria antiga – mantido pra compatibilidade/migração.
        public List<string> Gallery { get; set; } = new();

        // Até 30 fotos extras
        public List<string> Photos { get; set; } = new();

        // Até 20 vídeos de até 15s
        public List<string> Videos { get; set; } = new();

        // Tags de interesses selecionadas (ex: Música, Filmes, Viagem...)
        public List<string> Interests { get; set; } = new();

        // Status online
        public bool IsOnline { get; set; } = false;
        public long LastOnlineUtc { get; set; } = 0;
    }
}
