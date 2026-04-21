using System.Text.Json;
using System.Text.Json.Serialization;
using UnoGame.Core.Models;

namespace UnoGame.Core.Engine;

/// <summary>
/// Serialize/deserialize GameState sang JSON để lưu vào MongoDB.
/// GameService gọi Serialize trước khi save, Deserialize sau khi load.
///
/// Custom converter cho Card vì là sealed record với constructor có validation.
/// </summary>
public static class GameStateSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy    = JsonNamingPolicy.CamelCase,
        WriteIndented           = false,
        DefaultIgnoreCondition  = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(),
            new CardJsonConverter(),
            new PlayerStateJsonConverter()
        }
    };

    public static string Serialize(GameState state) =>
        JsonSerializer.Serialize(state, Options);

    public static GameState Deserialize(string json) =>
        JsonSerializer.Deserialize<GameState>(json, Options)
        ?? throw new InvalidOperationException("Failed to deserialize GameState");

    // ── Custom converter for Card (immutable record with validation) ────────

    private sealed class CardJsonConverter : JsonConverter<Card>
    {
        public override Card Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions opts)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            var color = Enum.Parse<CardColor>(root.GetProperty("color").GetString()!);
            var kind  = Enum.Parse<CardType> (root.GetProperty("type") .GetString()!);
            int? value = root.TryGetProperty("value", out var v) && v.ValueKind != JsonValueKind.Null
                ? v.GetInt32() : null;

            return new Card(color, kind, value);
        }

        public override void Write(Utf8JsonWriter writer, Card card, JsonSerializerOptions opts)
        {
            writer.WriteStartObject();
            writer.WriteString("color", card.Color.ToString());
            writer.WriteString("type",  card.Type.ToString());
            if (card.Value.HasValue) writer.WriteNumber("value", card.Value.Value);
            else writer.WriteNull("value");
            writer.WriteEndObject();
        }
    }

    // ── Custom converter for PlayerState (has Hand: List<Card>) ────────────

    private sealed class PlayerStateJsonConverter : JsonConverter<PlayerState>
    {
        private static readonly CardJsonConverter CardConv = new();

        public override PlayerState Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions opts)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            var player = new PlayerState(
                root.GetProperty("playerId").GetString()!,
                root.GetProperty("displayName").GetString()!,
                root.TryGetProperty("avatarUrl", out var av) ? av.GetString() ?? "" : "",
                root.TryGetProperty("isBot", out var ib) && ib.GetBoolean());

            // Hand
            if (root.TryGetProperty("hand", out var handProp))
                foreach (var cardElem in handProp.EnumerateArray())
                {
                    var cardReader = new Utf8JsonReader(
                        System.Text.Encoding.UTF8.GetBytes(cardElem.GetRawText()));
                    cardReader.Read();
                    player.AddCard(CardConv.Read(ref cardReader, typeof(Card), opts)!);
                }

            if (root.TryGetProperty("hasCalledUno",   out var hcu)) player.HasCalledUno   = hcu.GetBoolean();
            if (root.TryGetProperty("isConnected",    out var ic))  player.IsConnected    = ic.GetBoolean();
            if (root.TryGetProperty("turnsPlayed",    out var tp))  player.TurnsPlayed    = tp.GetInt32();
            if (root.TryGetProperty("penaltyDraws",   out var pd))  player.PenaltyDraws   = pd.GetInt32();

            return player;
        }

        public override void Write(Utf8JsonWriter writer, PlayerState p, JsonSerializerOptions opts)
        {
            writer.WriteStartObject();
            writer.WriteString("playerId",    p.PlayerId);
            writer.WriteString("displayName", p.DisplayName);
            writer.WriteString("avatarUrl",   p.AvatarUrl);
            writer.WriteBoolean("isBot",      p.IsBot);
            writer.WriteBoolean("hasCalledUno", p.HasCalledUno);
            writer.WriteBoolean("isConnected",  p.IsConnected);
            writer.WriteNumber("turnsPlayed",   p.TurnsPlayed);
            writer.WriteNumber("penaltyDraws",  p.PenaltyDraws);

            writer.WritePropertyName("hand");
            writer.WriteStartArray();
            foreach (var card in p.Hand)
                CardConv.Write(writer, card, opts);
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
    }
}
