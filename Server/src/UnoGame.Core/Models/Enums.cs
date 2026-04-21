namespace UnoGame.Core.Models;

// ════════════════════════════════════════════════════════════════
// UNO DOMAIN ENUMERATIONS
// Dùng chung ở Core, Infrastructure, API
// ════════════════════════════════════════════════════════════════

public enum CardColor
{
    Red    = 0,
    Green  = 1,
    Blue   = 2,
    Yellow = 3,
    Wild   = 4   // chỉ dùng cho Wild / WildDrawFour
}

public enum CardType
{
    Number       = 0,
    Skip         = 1,
    Reverse      = 2,
    DrawTwo      = 3,
    Wild         = 4,
    WildDrawFour = 5
}

public enum GamePhase
{
    /// <summary>Phòng đang chờ người chơi, chưa chia bài.</summary>
    Waiting  = 0,

    /// <summary>Game đang diễn ra.</summary>
    Playing  = 1,

    /// <summary>Game kết thúc — có người thắng.</summary>
    Ended    = 2
}

public enum RoomStatus
{
    Waiting = 0,
    Playing = 1,
    Closed  = 2
}

/// <summary>Kết quả của một lần gọi UNO.</summary>
public enum UnoCallResult
{
    /// <summary>Tự gọi UNO thành công khi còn 1 lá.</summary>
    SelfCalled  = 0,

    /// <summary>Bắt được đối thủ quên gọi UNO → nạn nhân rút 2 lá.</summary>
    Caught      = 1,

    /// <summary>Gọi không hợp lệ (target > 1 lá, hoặc đã gọi rồi).</summary>
    Invalid     = 2
}

/// <summary>Nguồn lá bài được rút.</summary>
public enum DrawSource
{
    Normal  = 0,   // rút thường (không đánh được)
    Penalty = 1,   // bị phạt do +2/+4 stack
}
