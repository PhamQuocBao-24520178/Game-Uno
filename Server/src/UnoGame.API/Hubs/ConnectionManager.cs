using System.Collections.Concurrent;

namespace UnoGame.API.Hubs;

/// <summary>
/// Theo dõi mapping: connectionId ↔ userId ↔ roomId.
/// Singleton — thread-safe với ConcurrentDictionary.
///
/// Dữ liệu lưu trong memory; nếu scale horizontal cần migrate sang Redis.
/// Xem README §Scaling để biết cách thay thế bằng IDistributedConnectionManager.
/// </summary>
public interface IConnectionManager
{
    // ── Registration ─────────────────────────────────────────────────────
    void   Register(string connectionId, string userId);
    void   Unregister(string connectionId);

    // ── Room membership ──────────────────────────────────────────────────
    void   JoinRoom(string connectionId, string roomId);
    void   LeaveRoom(string connectionId);
    string? GetRoomId(string connectionId);
    string? GetRoomIdByUser(string userId);

    // ── Lookups ──────────────────────────────────────────────────────────
    string? GetUserId(string connectionId);
    string? GetConnectionId(string userId);

    // ── Room queries ─────────────────────────────────────────────────────
    IReadOnlyList<string> GetConnectionsInRoom(string roomId);
    IReadOnlyList<string> GetUserIdsInRoom(string roomId);
    bool IsOnline(string userId);
    int  OnlineCountInRoom(string roomId);
}

public sealed class ConnectionManager : IConnectionManager
{
    // connectionId → userId
    private readonly ConcurrentDictionary<string, string> _connToUser = new();

    // userId → connectionId  (1 user = 1 active connection)
    private readonly ConcurrentDictionary<string, string> _userToConn = new();

    // connectionId → roomId
    private readonly ConcurrentDictionary<string, string> _connToRoom = new();

    // roomId → Set<connectionId>
    private readonly ConcurrentDictionary<string, ConcurrentHashSet<string>> _roomConns = new();

    // ── Registration ─────────────────────────────────────────────────────

    public void Register(string connectionId, string userId)
    {
        // Nếu user đã có connection cũ (reconnect), xoá connection cũ
        if (_userToConn.TryGetValue(userId, out var oldConnId) && oldConnId != connectionId)
        {
            _connToUser.TryRemove(oldConnId, out _);
            _connToRoom.TryRemove(oldConnId, out var oldRoomId);
            if (oldRoomId != null)
                _roomConns.GetValueOrDefault(oldRoomId)?.Remove(oldConnId);
        }

        _connToUser[connectionId] = userId;
        _userToConn[userId]       = connectionId;
    }

    public void Unregister(string connectionId)
    {
        if (_connToUser.TryRemove(connectionId, out var userId))
            _userToConn.TryRemove(userId, out _);

        if (_connToRoom.TryRemove(connectionId, out var roomId))
            _roomConns.GetValueOrDefault(roomId)?.Remove(connectionId);
    }

    // ── Room membership ──────────────────────────────────────────────────

    public void JoinRoom(string connectionId, string roomId)
    {
        // Rời phòng cũ trước
        if (_connToRoom.TryGetValue(connectionId, out var oldRoom) && oldRoom != roomId)
            _roomConns.GetValueOrDefault(oldRoom)?.Remove(connectionId);

        _connToRoom[connectionId] = roomId;
        _roomConns.GetOrAdd(roomId, _ => new ConcurrentHashSet<string>()).Add(connectionId);
    }

    public void LeaveRoom(string connectionId)
    {
        if (_connToRoom.TryRemove(connectionId, out var roomId))
            _roomConns.GetValueOrDefault(roomId)?.Remove(connectionId);
    }

    public string? GetRoomId(string connectionId) =>
        _connToRoom.GetValueOrDefault(connectionId);

    public string? GetRoomIdByUser(string userId)
    {
        var connId = GetConnectionId(userId);
        return connId is null ? null : GetRoomId(connId);
    }

    // ── Lookups ──────────────────────────────────────────────────────────

    public string? GetUserId(string connectionId) =>
        _connToUser.GetValueOrDefault(connectionId);

    public string? GetConnectionId(string userId) =>
        _userToConn.GetValueOrDefault(userId);

    // ── Room queries ─────────────────────────────────────────────────────

    public IReadOnlyList<string> GetConnectionsInRoom(string roomId) =>
        _roomConns.TryGetValue(roomId, out var set)
            ? set.ToList()
            : Array.Empty<string>();

    public IReadOnlyList<string> GetUserIdsInRoom(string roomId)
    {
        var conns = GetConnectionsInRoom(roomId);
        return conns
            .Select(c => _connToUser.GetValueOrDefault(c))
            .Where(u => u is not null)
            .Cast<string>()
            .ToList();
    }

    public bool IsOnline(string userId) => _userToConn.ContainsKey(userId);

    public int OnlineCountInRoom(string roomId) =>
        _roomConns.TryGetValue(roomId, out var set) ? set.Count : 0;
}

// ─── Minimal thread-safe HashSet ────────────────────────────────────────────

internal sealed class ConcurrentHashSet<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, byte> _dict = new();

    public void Add(T item)    => _dict[item] = 0;
    public void Remove(T item) => _dict.TryRemove(item, out _);
    public bool Contains(T item) => _dict.ContainsKey(item);
    public int Count => _dict.Count;
    public List<T> ToList() => _dict.Keys.ToList();
}
