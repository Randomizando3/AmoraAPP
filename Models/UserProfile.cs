using System.Collections.Generic;
using System.Text.Json.Serialization;

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

        // Telefone / celular
        public string PhoneNumber { get; set; } = string.Empty;

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

        // O que busca no app (multi-seleção: amizade, namoro, casamento, casual)
        public List<string> LookingFor { get; set; } = new();

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

        // Status online (para exibição no Discover)
        public bool IsOnline { get; set; } = false;
        public long LastOnlineUtc { get; set; } = 0;

        // ===== Localização =====

        public double Latitude { get; set; } = 0;
        public double Longitude { get; set; } = 0;
        public string CurrentLocationText { get; set; } = string.Empty;

        [JsonIgnore]
        public double DistanceKm { get; set; } = 0;

        // ===== Email =====
        public bool EmailVerified { get; set; } = false;


        /// <summary>
        /// Plano atual do usuário: "Free", "Plus" ou "Premium".
        /// Default = Free.
        /// </summary>
        public string Plan { get; set; } = "Free";

        // ===== BOOST / DESTAQUE =====

        /// <summary>
        /// Se true, o perfil está com boost ativo (destaque) no Discover.
        /// </summary>
        public bool IsBoostActive { get; set; } = false;

        /// <summary>
        /// Quando o boost termina (Unix time em segundos UTC).
        /// 0 = sem boost.
        /// </summary>
        public long BoostExpiresUtc { get; set; } = 0;

        /// <summary>
        /// Fator de prioridade enquanto o boost está ativo.
        /// Ex.: 5 para Plus, 10 para Premium, etc.
        /// </summary>
        public int BoostMultiplier { get; set; } = 1;

        /// <summary>
        /// Quantidade de boosts avulsos (pacotes adicionais).
        /// Usado para plano Free poder ativar boost consumindo 1 token.
        /// </summary>
        public int ExtraBoostTokens { get; set; } = 0;

        /// <summary>
        /// Quantas vezes o usuário usou boost no dia registrado em BoostUsesDayUtc.
        /// </summary>
        public int BoostUsesToday { get; set; } = 0;

        /// <summary>
        /// Dia (UTC) em que o contador BoostUsesToday vale.
        /// Armazenado como Unix time em segundos da data à meia-noite UTC.
        /// </summary>
        public long BoostUsesDayUtc { get; set; } = 0;


    }
}
