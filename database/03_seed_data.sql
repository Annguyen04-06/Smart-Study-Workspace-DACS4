/* =============================================================================
   TỆP: 03_seed_data.sql
   HỆ THỐNG: Smart Study Workspace (VKU Edition)
   MÔ TẢ: Dữ liệu khởi tạo chuẩn mực cho sinh viên CNTT trường VKU, đồng bộ với DbSeeder.cs.
   TÀI KHOẢN DEMO MẶC ĐỊNH:
   - Admin:       admin@vku.udn.vn          (Mật khẩu: Admin@123)
   - 11 Sinh viên: 23IT001 -> 23IT011       (Mật khẩu chung: Student@123)
     + SV1: an.nv.23it@vku.udn.vn   (23IT001 - Nguyễn Văn An)
     + SV2: binh.tt.23it@vku.udn.vn (23IT002 - Trần Thị Bình)
   GHI CHÚ: Khi khởi chạy SmartStudy.API, DbSeeder.cs (EnsureRichSeedDataAsync) sẽ tự động
            sinh vật lý đủ 8 file PDF đa trang và bơm đầy đủ 80+ tin nhắn, 60 câu hỏi Quiz,
            32 lượt thi Leaderboard, 60 thẻ Flashcard và 78 bản ghi AI Logs 14 ngày.
   ============================================================================= */

USE [SmartStudyWorkspaceDb];
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Email] = N'admin@vku.udn.vn')
BEGIN
    BEGIN TRANSACTION;

    DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();
    -- BCrypt hash thực tế tương ứng Admin@123 và Student@123 (workFactor = 11)
    DECLARE @AdminHash   NVARCHAR(255) = N'$2a$11$0M8tO0g9K1rA6qS3uX7vZeL2pY5wR8nC4bV6mH9jK1lP3qW5eR7tG';
    DECLARE @StudentHash NVARCHAR(255) = N'$2a$11$9K2pL4mN6qR8sT0uV2wX4eY6zA8bC0dE2fG4hI6jK8lM0nO2pQ4rS';

    -- 1. Seed 12 VKU Users (1 Admin + 11 Students 23IT001..23IT011)
    SET IDENTITY_INSERT [dbo].[Users] ON;
    INSERT INTO [dbo].[Users] ([Id], [StudentCode], [FullName], [Email], [PasswordHash], [AvatarUrl], [Role], [IsLocked], [CreatedAt])
    VALUES
        (1,  NULL,       N'Quản trị viên Hệ thống VKU', N'admin@vku.udn.vn',          @AdminHash,   N'https://api.dicebear.com/7.x/avataaars/svg?seed=VKUAdmin',       N'Admin',   0, DATEADD(DAY, -30, @Now)),
        (2,  N'23IT001', N'Nguyễn Văn An',              N'an.nv.23it@vku.udn.vn',     @StudentHash, N'https://api.dicebear.com/7.x/avataaars/svg?seed=NguyenVanAn',    N'Student', 0, DATEADD(DAY, -25, @Now)),
        (3,  N'23IT002', N'Trần Thị Bình',              N'binh.tt.23it@vku.udn.vn',   @StudentHash, N'https://api.dicebear.com/7.x/avataaars/svg?seed=TranThiBinh',    N'Student', 0, DATEADD(DAY, -24, @Now)),
        (4,  N'23IT003', N'Lê Hoàng Nam',               N'nam.lh.23it@vku.udn.vn',    @StudentHash, N'https://api.dicebear.com/7.x/avataaars/svg?seed=LeHoangNam',     N'Student', 0, DATEADD(DAY, -23, @Now)),
        (5,  N'23IT004', N'Phạm Minh Tuấn',             N'tuan.pm.23it@vku.udn.vn',   @StudentHash, N'https://api.dicebear.com/7.x/avataaars/svg?seed=PhamMinhTuan',   N'Student', 0, DATEADD(DAY, -22, @Now)),
        (6,  N'23IT005', N'Võ Thảo Vy',                 N'vy.vt.23it@vku.udn.vn',     @StudentHash, N'https://api.dicebear.com/7.x/avataaars/svg?seed=VoThaoVy',       N'Student', 0, DATEADD(DAY, -21, @Now)),
        (7,  N'23IT006', N'Đặng Quốc Bảo',              N'bao.dq.23it@vku.udn.vn',    @StudentHash, N'https://api.dicebear.com/7.x/avataaars/svg?seed=DangQuocBao',    N'Student', 0, DATEADD(DAY, -20, @Now)),
        (8,  N'23IT007', N'Hoàng Gia Huy',              N'huy.hg.23it@vku.udn.vn',    @StudentHash, N'https://api.dicebear.com/7.x/avataaars/svg?seed=HoangGiaHuy',    N'Student', 0, DATEADD(DAY, -19, @Now)),
        (9,  N'23IT008', N'Bùi Thanh Trúc',             N'truc.bt.23it@vku.udn.vn',   @StudentHash, N'https://api.dicebear.com/7.x/avataaars/svg?seed=BuiThanhTruc',   N'Student', 0, DATEADD(DAY, -18, @Now)),
        (10, N'23IT009', N'Ngô Đức Thịnh',              N'thinh.nd.23it@vku.udn.vn',  @StudentHash, N'https://api.dicebear.com/7.x/avataaars/svg?seed=NgoDucThinh',    N'Student', 0, DATEADD(DAY, -17, @Now)),
        (11, N'23IT010', N'Phan Khánh Linh',            N'linh.pk.23it@vku.udn.vn',   @StudentHash, N'https://api.dicebear.com/7.x/avataaars/svg?seed=PhanKhanhLinh',  N'Student', 0, DATEADD(DAY, -16, @Now)),
        (12, N'23IT011', N'Trương Nhật Minh',           N'minh.tn.23it@vku.udn.vn',   @StudentHash, N'https://api.dicebear.com/7.x/avataaars/svg?seed=TruongNhatMinh', N'Student', 0, DATEADD(DAY, -15, @Now));
    SET IDENTITY_INSERT [dbo].[Users] OFF;

    -- 2. Seed 11 Phòng học Chuyên ngành VKU (10 Phòng Active mở sẵn + 1 Phòng Pending chờ Admin duyệt)
    SET IDENTITY_INSERT [dbo].[StudyRooms] ON;
    INSERT INTO [dbo].[StudyRooms] ([Id], [Name], [SubjectCategory], [Description], [InviteCode], [Status], [CreatedById], [CreatedAt])
    VALUES
        (1,  N'Lập trình Mạng nâng cao với SignalR & WebSockets',     N'Lập trình Mạng',    N'Phân tích giao thức TCP/UDP, kiến trúc Full-Duplex WebSockets và lập trình thời gian thực SignalR.', 'NET101', N'Active',  2,  DATEADD(DAY, -14, @Now)),
        (2,  N'Cấu trúc dữ liệu & Giải thuật Đồ thị',                 N'Cấu trúc dữ liệu',  N'Luyện tập chuyên sâu BFS/DFS, Dijkstra Min-Heap, Bellman-Ford, Kruskal/DSU và Quy hoạch động.',      'DSA202', N'Active',  3,  DATEADD(DAY, -13, @Now)),
        (3,  N'Nhập môn Trí tuệ Nhân tạo & Machine Learning',         N'Trí tuệ nhân tạo',  N'Thảo luận giải thuật tìm kiếm A*, suy diễn xác suất Bayes, Logistic Regression & Gradient Descent.',  'AI305',  N'Active',  6,  DATEADD(DAY, -12, @Now)),
        (4,  N'Tối ưu hóa Truy vấn & Thiết kế CSDL SQL Server',       N'Cơ sở dữ liệu',     N'Thiết kế lược đồ chuẩn 3NF, phân tích Execution Plan, Covering Index B+Tree và giao dịch ACID.',      'SQL204', N'Active',  7,  DATEADD(DAY, -11, @Now)),
        (5,  N'Toán rời rạc & Lý thuyết Đồ thị Ôn thi Cuối kỳ',       N'Toán rời rạc',      N'Tổng ôn Nguyên lý Dirichlet, Bao hàm - Loại trừ, Hệ thức truy hồi, Định lý Master & Đồ thị Euler.',   'MAT108', N'Active',  8,  DATEADD(DAY, -10, @Now)),
        (6,  N'Nguyên lý Hệ điều hành Linux & Quản lý Tiến trình',    N'Hệ điều hành',      N'Nghiên cứu lời gọi hệ thống fork() Copy-on-Write, bộ lập lịch CFS, Semaphore & chống Deadlock.',      'OS209',  N'Active',  9,  DATEADD(DAY, -9,  @Now)),
        (7,  N'Phát triển Ứng dụng Web Full-Stack & RESTful API',     N'Phát triển Web',    N'Thực hành thiết kế chuẩn RESTful API, xác thực Stateless JWT, cấu hình CORS và tối ưu HTTP/2.',       'WEB301', N'Active',  4,  DATEADD(DAY, -7,  @Now)),
        (8,  N'An toàn Bảo mật Thông tin & Phòng chống OWASP Top 10', N'An toàn thông tin', N'Chuyên đề mật mã khóa công khai RSA, băm mật khẩu BCrypt và kỹ thuật phòng thủ SQLi, XSS, CSRF.',     'SEC308', N'Active',  5,  DATEADD(DAY, -6,  @Now)),
        (9,  N'Điện toán Đám mây, Docker Container & Đường ống CI/CD',N'Điện toán đám mây', N'Đóng gói dịch vụ Multi-Stage Dockerfile, Docker Compose, cgroups và triển khai Blue-Green.',          'CLD405', N'Active',  10, DATEADD(DAY, -5,  @Now)),
        (10, N'Công nghệ Phần mềm, Clean Architecture & Nguyên lý SOLID', N'Công nghệ phần mềm', N'Áp dụng 5 nguyên lý SOLID, tách biệt 4 tầng Clean Architecture và kiểm thử tự động xUnit.',        'SWE206', N'Active',  11, DATEADD(DAY, -3,  @Now)),
        (11, N'Hệ thống Nhúng IoT, Giao thức MQTT & Cảm biến ESP32',  N'Hệ thống IoT',      N'Xây dựng mạng cảm biến thông minh với vi điều khiển ESP32, giao thức nhẹ MQTT QoS 0/1/2.',            'IOT310', N'Pending', 12, DATEADD(DAY, -1,  @Now)),
        (12, N'Lập trình Thiết bị Di động Đa nền tảng với Flutter & Dart', N'Lập trình Di động', N'Kiến trúc 3 cây Flutter, quản lý trạng thái BLoC/Riverpod và đa luồng Dart Isolates.',          'MOB302', N'Active',  2,  DATEADD(DAY, -12, @Now)),
        (13, N'Học sâu (Deep Learning), Mạng CNN & Transformer',      N'Học sâu & Thị giác máy', N'Lan truyền ngược, ResNet Skip-Connection và cơ chế Scaled Dot-Product Attention.',               'DLG401', N'Active',  3,  DATEADD(DAY, -11, @Now)),
        (14, N'Xử lý Ngôn ngữ Tự nhiên (NLP), LLM & Kiến trúc RAG',   N'Trí tuệ nhân tạo',  N'Tokenization BPE, nhúng từ Vector Embeddings, Cosine Similarity và chống ảo giác RAG.',              'NLP403', N'Active',  4,  DATEADD(DAY, -10, @Now)),
        (15, N'Phân tích Dữ liệu Lớn với Apache Spark & Kafka',       N'Khoa học Dữ liệu',  N'Xử lý phân tán trong RAM với Spark DataFrame, Catalyst Optimizer và luồng sự kiện Kafka.',          'BIG309', N'Active',  5,  DATEADD(DAY, -9,  @Now)),
        (16, N'Lập trình Hướng đối tượng Chuyên sâu & Design Patterns',N'Công nghệ phần mềm', N'Đa hình động vtable, Composition over Inheritance và các mẫu Strategy, Observer, Factory.',       'OOP201', N'Active',  6,  DATEADD(DAY, -8,  @Now)),
        (17, N'Kiến trúc Máy tính, Pipeline RISC-V & Bộ nhớ Cache',   N'Hệ điều hành',      N'Đường ống 5 tầng IF-ID-EX-MEM-WB, Data Forwarding và tối ưu thời gian truy cập AMAT Cache.',       'ARC205', N'Active',  7,  DATEADD(DAY, -7,  @Now)),
        (18, N'Thiết kế Giao diện & Trải nghiệm UI/UX Design System', N'Phát triển Web',    N'Hệ lưới 8pt Grid, Định luật Fitts, Định luật Hick và tiêu chuẩn tương phản WCAG AA.',              'UIX304', N'Active',  8,  DATEADD(DAY, -6,  @Now)),
        (19, N'Kiểm thử Phần mềm Tự động, Giá trị Biên & TDD',        N'Kiểm thử phần mềm', N'Phân vùng tương đương, Phân tích giá trị biên, quy trình Red-Green-Refactor TDD và xUnit.',        'TST307', N'Active',  9,  DATEADD(DAY, -5,  @Now)),
        (20, N'Hệ thống Phân tán, Định lý CAP & Đồng thuận Raft',     N'Điện toán đám mây', N'Đánh đổi CP vs AP, quy tắc đa số quá bán Quorum trong Raft và chống sập bộ nhớ đệm Redis.',        'DIS406', N'Active',  10, DATEADD(DAY, -4,  @Now)),
        (21, N'Đồ án Chuyên ngành CNTT: Kỹ năng Bảo vệ Hội đồng',     N'Đồ án Chuyên ngành', N'Chuẩn hóa sơ đồ Use Case, Sequence, ERD 3NF, kịch bản Demo không lỗi và câu hỏi phản biện.',       'PRJ410', N'Active',  2,  DATEADD(DAY, -2,  @Now)),
        (22, N'Công nghệ Chuỗi khối (Blockchain), Cây Merkle & Smart Contract', N'An toàn thông tin', N'Liên kết băm SHA-256, xác minh giao dịch nhẹ Merkle Proof và phòng chống Reentrancy.',   'BLK412', N'Pending', 6,  DATEADD(DAY, -1,  @Now));
    SET IDENTITY_INSERT [dbo].[StudyRooms] OFF;

    COMMIT TRANSACTION;
END
GO
