# AGENTS.md — Architectural & Coding Standards for Smart Study Workspace

Tài liệu này là quy chuẩn kiến trúc bắt buộc cho toàn bộ AI Coding Agents khi đọc, sửa lỗi hoặc phát triển tính năng mới trên dự án **Smart Study Workspace**. Mọi thay đổi mã nguồn PHẢI tuân thủ tuyệt đối các quy tắc dưới đây.

---

## 1. Tổng quan Hệ thống & Cổng Dịch vụ (System Topology)

Dự án được tổ chức theo mô hình Monorepo gồm 4 thành phần độc lập:
- **`/database`**: Chứa các file script SQL Server tham chiếu chuẩn 3NF (`01_schema_tables.sql`, `02_indexes_constraints.sql`, `03_seed_data.sql`).
- **`/ai-service`** (Port `8000`): Python 3.11+ FastAPI Microservice chuyên bóc tách PDF và gọi Google Gemini API.
- **`/backend-core`** (Port `5000`): ASP.NET Core Web API (.NET 8 LTS) + SignalR Hub + EF Core 8 (Microsoft SQL Server).
- **`/frontend`** (Port `5500`): Web Application viết bằng HTML5 + Tailwind CSS (CDN) + Vanilla JavaScript (ES6 Modules).

---

## 2. Nguyên tắc Cốt lõi Toàn cục (Global Non-Negotiable Rules)

1. **Zero Placeholders & Zero Mock Data:**
   - Tuyệt đối KHÔNG để lại `// TODO`, `// Implement later`, `NotImplementedException`, hàm rỗng hoặc dữ liệu giả hardcode trên giao diện.
   - Khi thêm hoặc sửa một tính năng, phải hoàn thiện xuyên suốt từ Database -> Backend/AI -> Frontend.
2. **Clean Code & Dễ Bảo Vệ Đồ Án:**
   - Tuân thủ nghiêm ngặt **SOLID, DRY, KISS**. Mỗi hàm chỉ thực hiện một nhiệm vụ duy nhất (ngắn gọn, rõ ràng).
   - Đặt tên class, hàm, biến, file bằng tiếng Anh chuẩn xác, tường minh.
   - Viết comment tiếng Việt ngắn gọn ở đầu mỗi file/class và tại các đoạn thuật toán quan trọng (SignalR, JWT, gọi AI, chấm điểm Quiz, Spaced Repetition) để giải thích luồng hoạt động.
3. **Chuẩn Giao tiếp JSON Đồng nhất:**
   - Mọi REST API từ `/backend-core` bắt buộc trả về JSON định dạng `camelCase` bọc trong lớp `ApiResponse`:
     `{ "success": true, "message": "...", "data": ..., "errors": [] }`
   - Luôn đối chiếu chính xác tên trường JSON giữa DTO của C#, Schema của Python và file `*.api.js` / `*.view.js` của Frontend.

---

## 3. Quy tắc Tầng Backend Core (`/backend-core` — .NET 8 Clean Architecture)

### 3.1. Chiều Phụ thuộc Bắt buộc (Strict Dependency Rule)
- **`SmartStudy.Domain`**: Chỉ chứa Entities, Enums, Domain Exceptions. KHÔNG tham chiếu tới bất kỳ project hay thư viện hạ tầng nào (không EF Core, không Web, không JSON framework).
- **`SmartStudy.Application`**: Chứa Interfaces (`IAppDbContext`, `IAiMicroserviceClient`, `IStudyRoomRealtimeNotifier`...), DTOs, FluentValidation Validators và Business Services. Chỉ được tham chiếu `SmartStudy.Domain`.
- **`SmartStudy.Infrastructure`**: Chứa `AppDbContext`, `Configurations` (`IEntityTypeConfiguration`), `DbSeeder`, JWT Identity, Local File Storage, `PythonAiMicroserviceClient`. Tham chiếu `SmartStudy.Application` và `SmartStudy.Domain`.
- **`SmartStudy.API`**: Chỉ chứa Thin Controllers, SignalR Hubs (`StudyRoomHub`, `PresenceTracker`), `GlobalExceptionMiddleware` và `Program.cs`.
  - **CẤM TUYỆT ĐỐI** tiêm trực tiếp `AppDbContext` vào Controller hoặc viết truy vấn LINQ/SQL bên trong Controller. Controller chỉ nhận Request -> gọi Service ở tầng `Application` -> trả về `ApiResponse`.

### 3.2. Quy tắc Entity Framework Core & SQL Server
- Mỗi Entity bắt buộc có một file cấu hình riêng thực thi `IEntityTypeConfiguration` nằm trong `SmartStudy.Infrastructure/Persistence/Configurations/`. Không viết dồn cấu hình bảng vào hàm `OnModelCreating` của `AppDbContext`.
- **Chống lỗi Multiple Cascade Paths trên SQL Server:** Luôn thiết lập `DeleteBehavior.Restrict` hoặc `DeleteBehavior.NoAction` cho các khóa ngoại trỏ về `Users` hoặc `CourseMaterials` từ các bảng trung gian/bảng con (`StudyRooms`, `RoomMembers`, `ChatMessages`, `Quizzes`, `QuizAttempts`, `QuizAttemptAnswers`, `FlashcardDecks`, `AiUsageLogs`).
- Các cột chứa chuỗi tiếng Việt bắt buộc dùng kiểu `NVARCHAR`.

### 3.3. Quy tắc SignalR & Bất đồng bộ (`StudyRoomHub`)
- Xác thực JWT cho SignalR qua query string `access_token` tại `JwtBearerEvents.OnMessageReceived` cho đường dẫn `/hubs/study-room`.
- Khi xử lý tác vụ chạy ngầm gọi sang Python AI (như cú pháp `@bot`), BẮT BUỘC dùng `IServiceScopeFactory` tạo scope mới để lấy `IAppDbContext` và các Scoped Services; tuyệt đối không dùng lại `DbContext` của Hub sau khi method chính đã kết thúc để tránh lỗi `ObjectDisposedException`.
- Trong khối `try/catch/finally` của `@bot`, luôn đảm bảo phát sự kiện `BotThinking(false)` ở `finally` để giao diện người dùng không bao giờ bị treo trạng thái chờ.

---

## 4. Quy tắc Tầng AI Microservice (`/ai-service` — Layered FastAPI)

1. **Phân lớp Nghiêm ngặt:**
   - `app/core/`: Chỉ chứa cấu hình `.env` (`config.py`), kiểm tra header `X-Internal-Api-Key` (`security.py`) và custom exceptions.
   - `app/prompts/`: Nơi duy nhất lưu trữ toàn bộ System Instructions và Prompt Templates (`academic_prompts.py`). **CẤM** viết câu lệnh prompt dài trực tiếp bên trong router hay service.
   - `app/schemas/`: Định nghĩa Pydantic v2 Models cho Request, Response và `TokenUsageMetadata`.
   - `app/services/`: Xử lý trích xuất PDF (`pdfplumber`/`pypdf`) và gọi Gemini API.
   - `app/api/v1/endpoints/`: Chỉ định nghĩa HTTP route, gọi Dependency bảo mật `verify_internal_api_key` và ủy quyền xử lý xuống `app/services/`.
2. **Chuẩn Tích hợp Google Gemini:**
   - Bắt buộc sử dụng SDK mới `google-genai` (`from google import genai`) với model mặc định `gemini-2.5-flash`. Không dùng thư viện cũ đã ngừng hỗ trợ `google-generativeai`.
   - Mọi tính năng sinh dữ liệu có cấu trúc (Quiz, Flashcards) bắt buộc dùng **Structured Output** (`response_mime_type="application/json"` kèm Pydantic `response_schema`) để đảm bảo đúng cấu trúc JSON 100%.
   - Mọi phản hồi trả về cho C# Backend đều phải trích xuất đầy đủ `prompt_tokens`, `completion_tokens`, `total_tokens` từ `response.usage_metadata` kèm `duration_ms`.

---

## 5. Quy tắc Tầng Frontend (`/frontend` — Feature-Sliced ES6 MVC)

### 5.1. Ràng buộc Công nghệ (Strict Tech Constraints)
- Chỉ sử dụng **HTML5 + Tailwind CSS (CDN) + Vanilla JavaScript (chuẩn ES6 Modules với `type="module"`)**.
- **CẤM TUYỆT ĐỐI:** Không tự ý cài đặt React, Vue, Angular, TypeScript, jQuery, Webpack hay Vite bundler vào thư mục `/frontend`.
- **CẤM INLINE CODE:** Không viết logic JavaScript bên trong thẻ `<script>` nội tuyến của file `.html` (ngoại trừ cấu hình màu của Tailwind CDN và dòng nạp 1 file controller entry duy nhất), không dùng `onclick="..."` trực tiếp trong chuỗi HTML nếu có thể gắn qua Event Delegation chuẩn xác.

### 5.2. Kiến trúc Phân tách Module (API — View — Controller)
- **`pages/`**: Chỉ chứa khung giao diện HTML tĩnh sạch đẹp.
- **`src/core/`**:
  - Mọi URL gọi Backend, SignalR Hub và đường dẫn chuyển trang HTML phải lấy từ `src/core/config/app.config.js` (không hardcode chuỗi `"http://localhost:5000"` rải rác ở các file).
  - Mọi lệnh gọi REST API bắt buộc đi qua `src/core/http/httpClient.js` để tự động gắn Bearer Token và xử lý tập trung lỗi `401`/`403`.
  - Mọi kết nối thời gian thực đi qua singleton `src/core/realtime/signalrClient.js`.
- **`src/shared/`**: Chứa các thành phần dùng chung (`toast`, `modal`, `navbar`, `markdownViewer`) và hàm `escapeHtml()` chống tấn công XSS.
- **`src/features/<feature-name>/`**: Mỗi phân hệ nghiệp vụ bắt buộc tách thành 3 loại file riêng biệt:
  1. `*.api.js`: Chỉ chứa các hàm gọi `httpClient` tới Backend. Không thao tác DOM tại đây.
  2. `*.view.js`: Chỉ nhận dữ liệu thuần và trả về chuỗi HTML hoặc cập nhật giao diện cụ thể (luôn bọc dữ liệu người dùng nhập qua `escapeHtml()`, riêng nội dung `@bot` render qua `markdownViewer.component.js` kết hợp KaTeX và Highlight.js).
  3. `*.controller.js`: Khởi tạo trang, lắng nghe sự kiện DOM / SignalR, gọi `*.api.js` lấy dữ liệu và truyền sang `*.view.js` để hiển thị.

---

## 6. Đồng bộ Cơ sở Dữ liệu & Kiểm định Trước Khi Hoàn tất (Definition of Done)

Trước khi kết thúc bất kỳ yêu cầu chỉnh sửa hoặc thêm tính năng nào, Agent BẮT BUỘC thực hiện danh sách kiểm tra sau:

1. **Đồng bộ Database:** Nếu có thêm bảng hoặc sửa cột trong `SmartStudy.Domain/Entities`, phải cập nhật đồng thời file `IEntityTypeConfiguration`, `DbSeeder.cs` (nếu cần) VÀ cập nhật khớp 100% vào các file SQL trong `/database/`.
2. **Kiểm tra Biên dịch & Unit Test Backend:**
   - Chạy `dotnet build` tại `/backend-core` -> Đảm bảo **0 Error**.
   - Chạy `dotnet test` tại `/backend-core` -> Đảm bảo **Pass 100% tests**.
3. **Kiểm tra Cú pháp AI Service:**
   - Chạy `python -m py_compile` cho các file vừa sửa trong `/ai-service/app` -> Đảm bảo không lỗi cú pháp hay vòng lặp import.
4. **Kiểm tra Đường dẫn ES6 Module ở Frontend:**
   - Rà soát toàn bộ câu lệnh `import { ... } from '...'` trong các file `.js` phải có đầy đủ phần mở rộng `.js` ở cuối (ví dụ `import { httpClient } from '../../core/http/httpClient.js';`) để trình duyệt chạy trực tiếp ES6 Module không bị lỗi `404 Module Not Found`.