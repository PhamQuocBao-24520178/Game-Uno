# UnoGame

UnoGame là đồ án phát triển trò chơi UNO trên nền tảng desktop, được xây dựng bằng **Unity** và **C#**. Dự án mô phỏng luật chơi UNO cơ bản, đồng thời tích hợp các tính năng như quản lý tài khoản, hồ sơ người chơi, tạo phòng trực tuyến bằng mã phòng và hệ thống chat để giao tiếp giữa người chơi.

Dự án gồm hai thành phần chính:

* **Client:** Ứng dụng Unity chịu trách nhiệm hiển thị giao diện, xử lý gameplay, animation, âm thanh và tương tác người dùng.
* **Backend:** ASP.NET Core Web API xử lý các chức năng đăng ký, đăng nhập, quên mật khẩu, quản lý phòng chơi và hệ thống chat.

---

## 1. Giới thiệu Gameplay

UNO là trò chơi bài trong đó người chơi lần lượt đánh các lá bài có cùng màu hoặc cùng giá trị/ký hiệu với lá bài hiện tại trên bàn. Người chơi đánh hết bài trước sẽ là người chiến thắng.

Trong UnoGame, người chơi có thể:

* Đăng ký và đăng nhập tài khoản.
* Tạo hồ sơ cá nhân với tên hiển thị và avatar.
* Chơi với máy (Vs Computer).
* Tạo phòng chơi trực tuyến bằng mã phòng gồm 4 chữ số.
* Chia sẻ mã phòng thông qua hệ thống chat thế giới.
* Tham gia phòng chờ và đợi người chơi khác.
* Trải nghiệm giao diện chơi UNO trực quan.

### Các loại bài trong game

* Bài số theo màu: Đỏ, Xanh Dương, Vàng, Xanh Lá.
* Bài chức năng: Skip, Reverse, Draw +2.
* Bài đặc biệt: Wild, Wild +4.

### Các luật chơi đã được xử lý

* Chỉ cho phép đánh các lá bài hợp lệ.
* Người chơi phải rút bài nếu không có lá phù hợp.
* Wild và Wild +4 cho phép đổi màu.
* Xử lý phạt rút bài với lá +2 và +4.
* Hỗ trợ nút UNO khi người chơi còn ít bài.
* Bot tự động thực hiện lượt chơi.
* Hiển thị popup UNO và popup phạt rút bài.

---

## 2. Công nghệ sử dụng

| Thành phần           | Công nghệ                                |
| -------------------- | ---------------------------------------- |
| Game Client          | Unity 6, C#                              |
| Backend API          | ASP.NET Core Web API                     |
| Authentication       | JWT Token                                |
| Lưu dữ liệu online   | Firebase Realtime Database / Backend API |
| Gửi email            | Gmail SMTP / EmailService                |
| Public backend local | ngrok                                    |

---

## 3. Yêu cầu cài đặt

* Unity 6 hoặc phiên bản tương thích.
* Visual Studio hoặc Visual Studio Code.
* .NET SDK.
* Git.
* Postman hoặc Swagger.
* ngrok.
* Trình duyệt web.

---

## 4. Cấu trúc thư mục đề xuất

```text
UnoGame/
├── Client/                         # Unity project
│   ├── Assets/
│   │   ├── Scenes/
│   │   │   ├── StartMenu.unity
│   │   │   ├── ProfileSetting.unity
│   │   │   ├── Home.unity
│   │   │   ├── WaitingScene.unity
│   │   │   ├── Game.unity
│   │   │   ├── OnlineGame.unity
│   │   │   └── ResultGame.unity
│   │   │
│   │   ├── Scripts/
│   │   │   ├── Config/
│   │   │   │   └── ApiConfig.cs
│   │   │   │
│   │   │   ├── Auth/
│   │   │   │   ├── ApiModels.cs
│   │   │   │   ├── LoginPanelController.cs
│   │   │   │   ├── SignUpPanelController.cs
│   │   │   │   ├── ForgotPanelController.cs
│   │   │   │   └── UIMessage.cs
│   │   │   │
│   │   │   ├── Profile/
│   │   │   │   └── ProfileSettingController.cs
│   │   │   │
│   │   │   ├── Home/
│   │   │   │   ├── HomeController.cs
│   │   │   │   └── LogoutController.cs
│   │   │   │
│   │   │   ├── Online/
│   │   │   │   ├── OnlineRoomManager.cs
│   │   │   │   ├── RoomApiService.cs
│   │   │   │   ├── RoomModels.cs
│   │   │   │   ├── WaitingSceneController.cs
│   │   │   │   ├── OnlineGameSetupController.cs
│   │   │   │   ├── OnlineChatUIController.cs
│   │   │   │   ├── GlobalChatUIController.cs
│   │   │   │   ├── GlobalChatApiService.cs
│   │   │   │   └── GlobalChatModels.cs
│   │   │   │
│   │   │   ├── Game/
│   │   │   │   ├── GameSettingController.cs
│   │   │   │   ├── UnoCardData.cs
│   │   │   │   ├── UnoCardUI.cs
│   │   │   │   ├── UnoDeck.cs
│   │   │   │   ├── UnoGameController.cs
│   │   │   │   ├── UnoGameDealController.cs
│   │   │   │   ├── CardAnimationManager.cs
│   │   │   │   ├── TurnTimerRingManager.cs
│   │   │   │   ├── DrawPenaltyPopupManager.cs
│   │   │   │   └── UnoPopupManager.cs
│   │   │   │
│   │   │   └── Audio/
│   │   │       ├── SoundManager.cs
│   │   │       └── ButtonClickSound.cs
│   │   │
│   │   ├── Sprites/
│   │   ├── Prefabs/
│   │   ├── Sound/
│   │   └── TextMesh Pro/
│   │
│   └── ProjectSettings/
│
└── Server/
    └── UnoCustomBackend.Api/
        ├── Controllers/
        │   ├── AuthController.cs
        │   ├── ProfileController.cs
        │   ├── RoomsController.cs
        │   └── GlobalChatController.cs
        │
        ├── Models/
        │   ├── Entities/
        │   ├── Requests/
        │   └── Responses/
        │
        ├── Services/
        ├── Helpers/
        ├── Program.cs
        └── appsettings.json
```

---

## 5. Tính năng nổi bật

### 5.1. Hệ thống tài khoản

* Đăng ký tài khoản.
* Đăng nhập bằng username hoặc email.
* Lưu JWT Token sau khi đăng nhập.
* Quên mật khẩu qua email.
* Đặt lại mật khẩu.
* Đăng xuất tài khoản.

### 5.2. Hồ sơ người chơi

* Chuyển đến scene `ProfileSetting` khi đăng nhập lần đầu.
* Nhập tên hiển thị.
* Chọn avatar cá nhân.
* Tự động chuyển đến Home nếu hồ sơ đã tồn tại.
* Lưu dữ liệu bằng PlayerPrefs.

### 5.3. Giao diện Home

* Hiển thị avatar và tên người chơi.
* Chơi với máy.
* Chơi online.
* Nhập mã phòng để tham gia.
* Bật/tắt âm thanh và nhạc nền.
* Đăng xuất tài khoản.
* Truy cập Global Chat.

### 5.4. Chế độ chơi với máy

* Chia bài tự động.
* Hiển thị bài người chơi và bot.
* Quản lý lượt chơi.
* Xử lý đánh bài, rút bài và bài chức năng.
* Hiệu ứng âm thanh khi thao tác.
* Popup UNO và popup phạt.

### 5.5. Phòng chờ Online

* Tạo phòng trực tuyến.
* Sinh mã phòng ngẫu nhiên gồm 4 chữ số.
* Hiển thị mã phòng và thông tin HOST.
* Hiển thị các vị trí người chơi đang chờ.
* Hủy phòng và quay lại Home.
* Tự động chuyển sang màn chơi khi đủ người.

### 5.6. Chat thế giới

* Dùng chung giữa Home và WaitingScene.
* Chia sẻ mã phòng qua chat.
* Xem và tham gia phòng từ mã phòng được chia sẻ.
* Gửi tin nhắn văn bản.
* Gửi emoji.
* Lưu trữ tạm thời bằng API Backend.

### 5.7. Âm thanh và cài đặt

* Nhạc nền.
* Hiệu ứng âm thanh nút bấm.
* Âm thanh rút bài.
* Âm thanh đánh bài.
* Âm thanh UNO.
* Bật/tắt Music và Sound.
* Có mặt trong Home, Game và OnlineGame.

---

## 6. Hướng dẫn cài đặt và chạy dự án

### 6.1. Clone dự án

```bash
git clone <repository-url>
cd UnoGame
```

### 6.2. Chạy Backend ASP.NET Core

Mở thư mục:

```text
Server/UnoCustomBackend.Api/
```

Khởi động bằng lệnh:

```bash
dotnet run
```

Ví dụ địa chỉ local:

```text
https://localhost:7193
```

Hoặc theo cấu hình trong `launchSettings.json`.

### 6.3. Sử dụng ngrok

Nếu cần truy cập backend từ nhiều máy:

```bash
ngrok http --domain=detest-spindle-bonelike.ngrok-free.dev https://localhost:7193
```

Kiểm tra file:

```text
Assets/Scripts/Config/ApiConfig.cs
```

Ví dụ:

```csharp
public static class ApiConfig
{
    public const string BaseUrl = "https://detest-spindle-bonelike.ngrok-free.dev";

    public static string LoginUrl => BaseUrl + "/api/Auth/login";
    public static string RegisterUrl => BaseUrl + "/api/Auth/register";
    public static string ForgotPasswordUrl => BaseUrl + "/api/Auth/forgot-password";
    public static string ResetPasswordUrl => BaseUrl + "/api/Auth/reset-password";
}
```

### 6.4. Mở Unity Client

Mở thư mục:

```text
Client/
```

Sau đó mở bằng Unity Hub.

### 6.5. Kiểm tra Build Settings

Vào:

```text
File → Build Settings
```

Đảm bảo các scene sau đã được thêm:

```text
StartMenu
ProfileSetting
Home
WaitingScene
Game
OnlineGame
ResultGame
```

### 6.6. Chạy game

Quy trình kiểm thử đề xuất:

```text
1. Chạy Backend.
2. Chạy ngrok (nếu cần).
3. Mở Unity.
4. Chạy StartMenu hoặc Home.
5. Đăng ký hoặc đăng nhập.
6. Tạo hồ sơ nếu là tài khoản mới.
7. Truy cập Home.
8. Chơi với máy để kiểm thử gameplay.
9. Tạo phòng online.
10. Chia sẻ mã phòng qua chat.
11. Máy khác nhập mã phòng để tham gia.
```

---

## 8. Lưu ý khi chạy

* Đảm bảo Backend đang hoạt động trước khi chạy Unity.
* Khi kiểm thử nhiều máy, cần sử dụng đúng domain ngrok.
* Kiểm tra route API nếu gặp lỗi 404.
* Kiểm tra SoundManager và AudioSource nếu âm thanh không hoạt động.
* Kiểm tra Emoji Sprites nếu emoji không hiển thị.
* Kiểm tra PlayerPrefs nếu mã phòng không được lưu.
* Không nên chạy trực tiếp WaitingScene vì scene này cần dữ liệu được truyền từ Home.

---

## 9. Hướng phát triển trong tương lai

* Đồng bộ gameplay online theo thời gian thực.
* Lưu trữ chat bằng cơ sở dữ liệu.
* Hiển thị đầy đủ thông tin người chơi trong phòng chờ.
* Hoàn thiện chế độ OnlineGame nhiều người chơi.
* Thêm bảng xếp hạng và kết quả trận đấu.
* Cải thiện animation chia bài và đánh bài.
* Nâng cấp hệ thống âm thanh.
* Tối ưu giao diện trên nhiều độ phân giải.
* Tích hợp WebSocket hoặc SignalR cho chế độ realtime.

---

