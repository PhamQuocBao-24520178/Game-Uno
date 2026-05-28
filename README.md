# UNO Boardgame — Multiplayer

Game UNO multiplayer 2–4 người chơi với bot AI, real-time qua SignalR.

---

## Tech Stack

| Layer | Công nghệ |
|---|---|
| Server | ASP.NET Core 10 · SignalR · Clean Architecture |
| Database | MongoDB 7 |
| Authentication | Firebase Authentication (JWT) |
| Real-time | SignalR WebSocket |
| Cache | Redis (optional — SignalR scale-out backplane) |
| Container | Docker · Docker Compose |

---

### Cấu trúc thư mục

```
uno_boardgame/
├── Server/
│   ├── UnoGame.slnx
│   ├── src/
│   │   ├── UnoGame.Core/
│   │   │   ├── Models/          Card, Player, GameState, Enums
│   │   │   ├── Engine/          Deck, RuleValidator, TurnManager, GameEngine, Serializer
│   │   │   ├── Bot/             UnoBot, CardScorer, BotMemory, ThreatAnalyzer, ColorPicker
│   │   │   ├── DTOs/            AllDtos, AuthDtos
│   │   │   ├── Interfaces/      IServices 
│   │   │   └── Room/            RoomRuntimeState, MatchmakingTicket, DisconnectRecord
│   │   ├── UnoGame.Infrastructure/
│   │   │   ├── Repositories/    MongoDB documents + repositories
│   │   │   └── Services/        AuthService, GameService, UserService, RoomService, LeaderboardService
│   │   └── UnoGame.API/
│   │       ├── Controllers/     Auth, User, Room, Game, Leaderboard 
│   │       ├── Hubs/            GameHub, BotOrchestrator, ConnectionManager
│   │       ├── Middleware/      FirebaseAuth, GlobalException, RateLimit
│   │       └── Services/        RoomManager
│   └── tests/
│       ├── UnoGame.Core.Tests/ 
│       └── UnoGame.API.Tests/
├── docker-compose.yml     
├── docker-compose.dev.yml     
├── .env.example
└── README.md
```

---

## Yêu cầu

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Firebase project với Email/Password Authentication đã bật

---

## Thiết lập & Chạy

### 1. Cấu hình môi trường

```bash
cp .env.example .env
# Điền FIREBASE_PROJECT_ID vào .env
```

### 2. Firebase Service Account

Tải từ Firebase Console → Project Settings → Service Accounts → Generate new private key,
đặt vào `Server/src/UnoGame.API/firebase-service-account.json`.

Cập nhật `Server/src/UnoGame.API/appsettings.json` với `ProjectId` và `WebApiKey` của project.

### 3. Khởi động infrastructure

```bash
# MongoDB + Redis
docker compose up mongo redis -d

# Thêm Mongo Express UI tại http://localhost:8081
docker compose -f docker-compose.yml -f docker-compose.dev.yml up mongo redis mongo-express -d
```

### 4. Chạy server

```bash
cd Server/src/UnoGame.API
dotnet watch run
# API:  http://localhost:5000
# Docs: http://localhost:5000/openapi/v1.json  (dán vào https://editor.swagger.io)
```

### 5. Chạy tests

```bash
dotnet test Server/UnoGame.slnx --logger "console;verbosity=normal"
```


---
