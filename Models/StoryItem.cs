using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AmoraApp.Models
{
    public class StoryItem
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;

        // ✅ FIX: aceita Unix ms/seg e ISO string
        [JsonConverter(typeof(FlexibleDateTimeConverter))]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ✅ FIX: normalmente vem no mesmo formato do CreatedAt
        [JsonConverter(typeof(FlexibleDateTimeConverter))]
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);

        // Likes simples por enquanto (contador)
        public int Likes { get; set; }
    }

    /// <summary>
    /// Aceita DateTime vindo como:
    /// - Number: Unix epoch em milissegundos (ex: 1768188243138) ou em segundos (ex: 1768162346)
    /// - String ISO (ex: 2026-01-12T00:00:00Z)
    /// - String numérica (ms/seg)
    /// - Null
    /// </summary>
    internal sealed class FlexibleDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try
            {
                if (reader.TokenType == JsonTokenType.Number)
                {
                    if (reader.TryGetInt64(out long n))
                        return FromEpochFlexible(n);

                    double d = reader.GetDouble();
                    return FromEpochFlexible((long)d);
                }

                if (reader.TokenType == JsonTokenType.String)
                {
                    var s = reader.GetString();
                    if (string.IsNullOrWhiteSpace(s)) return DateTime.UtcNow;

                    if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n))
                        return FromEpochFlexible(n);

                    if (DateTime.TryParse(
                            s,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out var dt))
                        return dt;

                    return DateTime.UtcNow;
                }

                if (reader.TokenType == JsonTokenType.Null)
                    return DateTime.UtcNow;

                // Se vier algo inesperado (objeto/array), consome e não quebra o app.
                using var _ = JsonDocument.ParseValue(ref reader);
                return DateTime.UtcNow;
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime());
        }

        private static DateTime FromEpochFlexible(long n)
        {
            // Heurística: > 10^12 é ms, senão segundos
            if (n > 1_000_000_000_000L)
                return DateTimeOffset.FromUnixTimeMilliseconds(n).UtcDateTime;

            return DateTimeOffset.FromUnixTimeSeconds(n).UtcDateTime;
        }
    }
}
