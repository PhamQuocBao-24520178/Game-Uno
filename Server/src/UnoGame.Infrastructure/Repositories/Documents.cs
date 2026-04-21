using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UnoGame.Core.Models;

namespace UnoGame.Infrastructure.Repositories;

// ════════════════════════════════════════════════════════════════════════════
// MONGODB DOCUMENT MODELS
// ════════════════════════════════════════════════════════════════════════════

[BsonIgnoreExtraElements]
public class UserDocument
{
    [BsonId, BsonRepresentation(BsonType.String)]
    public string   Id            { get; set; } = null!;  // Firebase UID
    public string   Email         { get; set; } = null!;
    public string   DisplayName   { get; set; } = null!;
    public string   AvatarUrl     { get; set; } = "";
    public int      GamesPlayed   { get; set; }
    public int      GamesWon      { get; set; }
    public int      TotalScore    { get; set; }
    public int      WeeklyScore   { get; set; }
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime LastPlayedAt  { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public class RoomDocument
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string   Id            { get; set; } = ObjectId.GenerateNewId().ToString();
    public string   HostId        { get; set; } = null!;
    public string?  HostName      { get; set; }
    public string   RoomCode      { get; set; } = null!;
    public string?  RoomName      { get; set; }
    public RoomStatus Status      { get; set; } = RoomStatus.Waiting;
    public int      MaxPlayers    { get; set; } = 4;
    public int      BotCount      { get; set; }
    public string   BotDifficulty { get; set; } = "hard";
    public bool     IsPrivate     { get; set; }
    public string?  Password      { get; set; }
    public int      MaxRounds     { get; set; } = 1;
    public List<string> PlayerIds { get; set; } = new();

    /// <summary>userId → isReady mapping</summary>
    public Dictionary<string, bool> ReadyStatus { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
