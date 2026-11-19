using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Domain.Entities;
using SmartStudy.Domain.Enums;

namespace SmartStudy.Infrastructure.Persistence.Seed;

/// <summary>
/// Cơ chế Seed Data thông minh nâng cấp toàn diện cho sinh viên CNTT trường VKU:
/// - Tự động đồng bộ cột mới (ví dụ ChatMessages.IsPinned) trên CSDL hiện hữu một cách an toàn.
/// - Sinh vật lý đầy đủ 8 tệp giáo trình PDF đa trang vào wwwroot/uploads/materials/ chống lỗi 404.
/// - Bơm dữ liệu phong phú: 12 Users (1 Admin + 11 SV VKU), 6 Phòng học (5 Active + 1 Pending),
///   8 Tài liệu chuyên sâu, 80+ Tin nhắn (có ghim & @bot KaTeX/Code/Table), 6 Đề Quiz (60 câu),
///   32 Lượt thi đua Top Leaderboard, 6 Bộ Flashcard (60 thẻ) và 78 Nhật ký AI rải đều 14 ngày.
/// </summary>
public static class DbSeeder
{
    public const string AdminDefaultPassword = "Admin@123";
    public const string StudentDefaultPassword = "Student@123";

    public static async Task SeedAsync(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        string webRootPath,
        ILogger logger)
    {
        if (context.Database.GetMigrations().Any())
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        await EnsureSchemaUpgradesOnSqlServerAsync(context);

        var materialsDir = Path.Combine(webRootPath, "uploads", "materials");
        var avatarsDir = Path.Combine(webRootPath, "uploads", "avatars");
        Directory.CreateDirectory(materialsDir);
        Directory.CreateDirectory(avatarsDir);

        var materialCatalog = VkuSeedCatalog.GetMaterials();
        var fileSizes = new Dictionary<string, long>();
        foreach (var item in materialCatalog)
        {
            var size = SamplePdfGenerator.EnsureMultiPagePdfExists(
                materialsDir,
                new SamplePdfGenerator.PdfSeedSpec(item.FileName, item.CourseCode, item.Title, item.Sections));
            fileSizes[item.FileName] = size;
        }

        // Nếu hệ thống đã có đủ >= 11 phòng học nền tảng VKU thì nâng cấp cộng dồn lên 22 phòng (giữ nguyên 100% tài khoản thật của người dùng!)
        var existingRoomCount = await context.StudyRooms.CountAsync();
        var hasVkuAdmin = await context.Users.AnyAsync(u => u.Email == "admin@vku.udn.vn");
        if (existingRoomCount >= 11 && hasVkuAdmin)
        {
            await EnsureExpanded22RoomsAdditiveAsync(context, passwordHasher, materialsDir, logger);
            await EnsureMindmapAndReactionsBackfillAsync(context, logger);
            return;
        }

        logger.LogInformation("Đang nâng cấp và bơm bộ dữ liệu học thuật VKU mở rộng 11 phòng học (EnsureRichSeedDataAsync)...");

        await ClearSparseDataSafelyAsync(context);

        var now = DateTime.UtcNow;
        var adminHash = passwordHasher.HashPassword(AdminDefaultPassword);
        var studentHash = passwordHasher.HashPassword(StudentDefaultPassword);

        // 1. Khởi tạo 12 Tài khoản VKU (1 Admin + 11 Sinh viên CNTT khóa 23IT)
        var admin = new User
        {
            StudentCode = null,
            FullName = "Quản trị viên Hệ thống VKU",
            Email = "admin@vku.udn.vn",
            PasswordHash = adminHash,
            AvatarUrl = "https://api.dicebear.com/7.x/avataaars/svg?seed=VKUAdmin",
            Role = UserRole.Admin,
            IsLocked = false,
            CreatedAt = now.AddDays(-30)
        };

        var studentSpecs = new (string Code, string Name, string Email, string SeedName)[]
        {
            ("23IT001", "Nguyễn Văn An",   "an.nv.23it@vku.udn.vn",    "NguyenVanAn"),
            ("23IT002", "Trần Thị Bình",   "binh.tt.23it@vku.udn.vn",  "TranThiBinh"),
            ("23IT003", "Lê Hoàng Nam",    "nam.lh.23it@vku.udn.vn",   "LeHoangNam"),
            ("23IT004", "Phạm Minh Tuấn",  "tuan.pm.23it@vku.udn.vn",  "PhamMinhTuan"),
            ("23IT005", "Võ Thảo Vy",      "vy.vt.23it@vku.udn.vn",    "VoThaoVy"),
            ("23IT006", "Đặng Quốc Bảo",   "bao.dq.23it@vku.udn.vn",   "DangQuocBao"),
            ("23IT007", "Hoàng Gia Huy",   "huy.hg.23it@vku.udn.vn",   "HoangGiaHuy"),
            ("23IT008", "Bùi Thanh Trúc",  "truc.bt.23it@vku.udn.vn",  "BuiThanhTruc"),
            ("23IT009", "Ngô Đức Thịnh",   "thinh.nd.23it@vku.udn.vn", "NgoDucThinh"),
            ("23IT010", "Phan Khánh Linh", "linh.pk.23it@vku.udn.vn",  "PhanKhanhLinh"),
            ("23IT011", "Trương Nhật Minh","minh.tn.23it@vku.udn.vn",  "TruongNhatMinh")
        };

        var students = studentSpecs.Select((s, idx) => new User
        {
            StudentCode = s.Code,
            FullName = s.Name,
            Email = s.Email,
            PasswordHash = studentHash,
            AvatarUrl = $"https://api.dicebear.com/7.x/avataaars/svg?seed={s.SeedName}",
            Role = UserRole.Student,
            IsLocked = false,
            CreatedAt = now.AddDays(-25 + idx)
        }).ToList();

        context.Users.Add(admin);
        context.Users.AddRange(students);
        await context.SaveChangesAsync();

        // 2. Khởi tạo 11 Phòng học Chuyên ngành VKU (10 Phòng Active mở sẵn + 1 Phòng Pending chờ Admin duyệt)
        var rooms = new List<StudyRoom>
        {
            new()
            {
                Name = "Lập trình Mạng nâng cao với SignalR & WebSockets",
                SubjectCategory = "Lập trình Mạng",
                Description = "Phân tích giao thức TCP/UDP, kiến trúc Full-Duplex WebSockets, PresenceTracker và lập trình thời gian thực với ASP.NET Core SignalR.",
                InviteCode = "NET101",
                Status = RoomStatus.Active,
                CreatedById = students[0].Id,
                CreatedAt = now.AddDays(-14)
            },
            new()
            {
                Name = "Cấu trúc dữ liệu & Giải thuật Đồ thị",
                SubjectCategory = "Cấu trúc dữ liệu",
                Description = "Luyện tập chuyên sâu BFS/DFS, Dijkstra Min-Heap, Bellman-Ford, Cây khung nhỏ nhất Kruskal/DSU và Quy hoạch động.",
                InviteCode = "DSA202",
                Status = RoomStatus.Active,
                CreatedById = students[1].Id,
                CreatedAt = now.AddDays(-13)
            },
            new()
            {
                Name = "Nhập môn Trí tuệ Nhân tạo & Machine Learning",
                SubjectCategory = "Trí tuệ nhân tạo",
                Description = "Thảo luận giải thuật tìm kiếm A*, suy diễn xác suất Bayes, Hồi quy tuyến tính/Logistic và tối ưu Gradient Descent.",
                InviteCode = "AI305",
                Status = RoomStatus.Active,
                CreatedById = students[4].Id,
                CreatedAt = now.AddDays(-12)
            },
            new()
            {
                Name = "Tối ưu hóa Truy vấn & Thiết kế CSDL SQL Server",
                SubjectCategory = "Cơ sở dữ liệu",
                Description = "Thiết kế lược đồ chuẩn 3NF, phân tích Execution Plan, Covering Index B+Tree và kiểm soát đồng thời ACID.",
                InviteCode = "SQL204",
                Status = RoomStatus.Active,
                CreatedById = students[5].Id,
                CreatedAt = now.AddDays(-11)
            },
            new()
            {
                Name = "Toán rời rạc & Lý thuyết Đồ thị Ôn thi Cuối kỳ",
                SubjectCategory = "Toán rời rạc",
                Description = "Tổng ôn Nguyên lý Dirichlet, Bao hàm - Loại trừ, Hệ thức truy hồi, Định lý Master và Chu trình Euler/Hamilton.",
                InviteCode = "MAT108",
                Status = RoomStatus.Active,
                CreatedById = students[6].Id,
                CreatedAt = now.AddDays(-10)
            },
            new()
            {
                Name = "Nguyên lý Hệ điều hành Linux & Quản lý Tiến trình",
                SubjectCategory = "Hệ điều hành",
                Description = "Nghiên cứu lời gọi hệ thống fork() Copy-on-Write, bộ lập lịch CFS Red-Black Tree, Semaphore và phòng chống Deadlock.",
                InviteCode = "OS209",
                Status = RoomStatus.Active,
                CreatedById = students[7].Id,
                CreatedAt = now.AddDays(-9)
            },
            new()
            {
                Name = "Phát triển Ứng dụng Web Full-Stack & RESTful API",
                SubjectCategory = "Phát triển Web",
                Description = "Thực hành thiết kế chuẩn RESTful API, xác thực Stateless JWT, cấu hình bảo mật CORS, ETag Caching và tối ưu HTTP/2.",
                InviteCode = "WEB301",
                Status = RoomStatus.Active,
                CreatedById = students[2].Id,
                CreatedAt = now.AddDays(-7)
            },
            new()
            {
                Name = "An toàn Bảo mật Thông tin & Phòng chống OWASP Top 10",
                SubjectCategory = "An toàn thông tin",
                Description = "Chuyên đề mật mã khóa công khai RSA, băm mật khẩu một chiều BCrypt và kỹ thuật phòng thủ SQL Injection, XSS, CSRF.",
                InviteCode = "SEC308",
                Status = RoomStatus.Active,
                CreatedById = students[3].Id,
                CreatedAt = now.AddDays(-6)
            },
            new()
            {
                Name = "Điện toán Đám mây, Docker Container & Đường ống CI/CD",
                SubjectCategory = "Điện toán đám mây",
                Description = "Đóng gói dịch vụ với Multi-Stage Dockerfile, Docker Compose, quản trị tài nguyên cgroups và triển khai Blue-Green không gián đoạn.",
                InviteCode = "CLD405",
                Status = RoomStatus.Active,
                CreatedById = students[8].Id,
                CreatedAt = now.AddDays(-5)
            },
            new()
            {
                Name = "Công nghệ Phần mềm, Clean Architecture & Nguyên lý SOLID",
                SubjectCategory = "Công nghệ phần mềm",
                Description = "Áp dụng 5 nguyên lý SOLID, tách biệt 4 tầng Clean Architecture và xây dựng bộ kiểm thử tự động xUnit thực chiến.",
                InviteCode = "SWE206",
                Status = RoomStatus.Active,
                CreatedById = students[9].Id,
                CreatedAt = now.AddDays(-3)
            },
            new()
            {
                Name = "Hệ thống Nhúng IoT, Giao thức MQTT & Cảm biến ESP32",
                SubjectCategory = "Hệ thống IoT",
                Description = "Xây dựng mạng cảm biến thông minh với vi điều khiển ESP32, giao thức nhẹ MQTT QoS 0/1/2 và tối ưu năng lượng Deep Sleep.",
                InviteCode = "IOT310",
                Status = RoomStatus.Pending,
                CreatedById = students[10].Id,
                CreatedAt = now.AddDays(-1)
            }
        };

        context.StudyRooms.AddRange(rooms);
        await context.SaveChangesAsync();

        // 3. Phân bổ Thành viên thông minh (để mỗi tài khoản Demo có ~5 phòng đã vào và ~5 phòng mở sẵn để bấm Tham gia / nhập Mã mời)
        int[][] roomMemberIndices =
        [
            [0, 1, 2, 3, 4, 5, 6, 7, 8],          // 0. NET101: 9 TV (An & Bình đã tham gia)
            [1, 0, 2, 3, 5, 6, 7, 9],             // 1. DSA202: 8 TV (An & Bình đã tham gia)
            [4, 0, 1, 2, 3, 6, 7, 8, 9, 10],      // 2. AI305:  10 TV (An & Bình đã tham gia)
            [5, 0, 2, 3, 7, 8, 10],               // 3. SQL204: 7 TV (An đã vào; Bình có thể bấm Tham gia / nhập mã SQL204)
            [6, 1, 4, 5, 9, 10],                  // 4. MAT108: 6 TV (Bình đã vào; An có thể nhập mã MAT108)
            [7, 3, 4, 6, 8, 10],                  // 5. OS209:  6 TV (Mở sẵn để cả An & Bình test nhập mã mời OS209!)
            [2, 1, 4, 6, 8, 9],                   // 6. WEB301: 6 TV (Bình đã vào; An có thể nhập mã WEB301)
            [3, 2, 5, 7, 8, 9, 10],               // 7. SEC308: 7 TV (Mở sẵn để cả An & Bình test Tham gia / mã SEC308)
            [8, 2, 4, 6, 7, 10],                  // 8. CLD405: 6 TV (Mở sẵn để cả An & Bình test Tham gia / mã CLD405)
            [9, 0, 3, 5, 7, 10],                  // 9. SWE206: 6 TV (An đã vào; Bình có thể nhập mã SWE206)
            [10, 4, 6, 8, 9]                      // 10. IOT310: 5 TV (Pending chờ Admin duyệt, vẫn vào được bằng mã IOT310)
        ];

        for (var rIdx = 0; rIdx < rooms.Count; rIdx++)
        {
            var memberList = roomMemberIndices[rIdx];
            for (var pos = 0; pos < memberList.Length; pos++)
            {
                var stu = students[memberList[pos]];
                context.RoomMembers.Add(new RoomMember
                {
                    RoomId = rooms[rIdx].Id,
                    UserId = stu.Id,
                    RoleInRoom = pos == 0 ? RoomMemberRole.Owner : RoomMemberRole.Member,
                    JoinedAt = rooms[rIdx].CreatedAt.AddHours(pos * 2)
                });
            }
        }
        await context.SaveChangesAsync();

        // 4. Lưu 8 Học liệu PDF vào CSDL khớp với 8 tệp PDF vật lý vừa tạo
        var courseMaterials = materialCatalog.Select((m, idx) => new CourseMaterial
        {
            RoomId = rooms[m.RoomIndex].Id,
            UploadedById = students[m.UploaderIndex].Id,
            Title = m.Title,
            OriginalFileName = m.FileName,
            StoredFilePath = $"/uploads/materials/{m.FileName}",
            FileSizeBytes = fileSizes.GetValueOrDefault(m.FileName, 24500),
            PageCount = m.LogicalPageCount,
            ExtractedText = string.Join("\n\n", m.Sections),
            AiSummary = m.AiSummary,
            IsApproved = true,
            CreatedAt = now.AddDays(-12 + idx)
        }).ToList();

        context.CourseMaterials.AddRange(courseMaterials);
        await context.SaveChangesAsync();

        // 5. Lưu hơn 80+ Tin nhắn hội thoại tự nhiên (bao gồm các tin nhắn @bot ghim kiến thức)
        // Bổ sung thêm các lượt trao đổi ôn tập sôi nổi giữa các thành viên để đủ >85 tin nhắn
        var chatMessages = VkuQuizAndChatSeedCatalog.BuildRichRoomConversations(rooms, students, now);
        string[] extraStudyPrompts =
        [
            "Mình vừa xem lại phần tóm tắt PDF ở cột phải, công nhận nắm ý chính nhanh gấp 3 lần đọc chay!",
            "Có bạn nào đang online bật Pomodoro 25 phút học chung với mình không?",
            "Mình vừa hoàn thành lượt thi thử mới, điểm trung bình tăng lên đáng kể rồi.",
            "Nhớ xem kỹ thanh Tin nhắn đã ghim ở đầu khung chat nha, công thức chuẩn nằm hết trên đó.",
            "Bộ thẻ 3D dùng phím Space để lật rồi bấm phím 1-2-3 đánh giá mức nhớ tiện thật sự!",
            "Tối thứ 7 tuần này cả nhóm tổng duyệt đồ án cuối kỳ trên phòng này luôn nhé."
        ];

        for (var rIdx = 0; rIdx < 10; rIdx++)
        {
            var mIndices = roomMemberIndices[rIdx];
            for (var k = 0; k < extraStudyPrompts.Length; k++)
            {
                var sender = students[mIndices[(k + 1) % mIndices.Length]];
                chatMessages.Add(new ChatMessage
                {
                    RoomId = rooms[rIdx].Id,
                    SenderId = sender.Id,
                    IsAiBot = false,
                    IsPinned = false,
                    Content = extraStudyPrompts[k],
                    CreatedAt = now.AddMinutes(-(95 - k * 11 - rIdx))
                });
            }
        }

        context.ChatMessages.AddRange(chatMessages.OrderBy(m => m.CreatedAt));
        await context.SaveChangesAsync();

        // 6. Lưu 10 Đề thi Quiz cho cả 10 phòng học Active
        var quizzes = VkuQuizAndChatSeedCatalog.BuildSixQuizzes(rooms, courseMaterials, students, now);
        context.Quizzes.AddRange(quizzes);
        await context.SaveChangesAsync();

        // 7. Lưu 48 Lượt làm bài QuizAttempts phân bổ cho các phòng học
        var attemptPlan = new (int QuizIdx, int StudentIdx, int CorrectCount, int SecondsSpent, int HoursAgo)[]
        {
            // Phòng NET101 (Quiz 0 - 10 câu)
            (0, 0, 10, 410, 96), (0, 7, 9, 445, 90), (0, 1, 9, 480, 84), (0, 2, 8, 510, 78),
            (0, 3, 8, 530, 72),  (0, 4, 7, 560, 68), (0, 5, 7, 590, 60), (0, 6, 6, 610, 52),
            // Phòng DSA202 (Quiz 1 - 10 câu)
            (1, 7, 10, 460, 88), (1, 1, 9, 495, 82), (1, 0, 9, 505, 76), (1, 3, 8, 540, 70),
            (1, 2, 8, 555, 64),  (1, 5, 7, 600, 58), (1, 9, 7, 620, 48),
            // Phòng AI305 (Quiz 2 - 10 câu)
            (2, 4, 10, 390, 85), (2, 0, 9, 430, 79), (2, 9, 9, 455, 73), (2, 10, 8, 490, 66),
            (2, 8, 8, 515, 59),  (2, 1, 7, 545, 50), (2, 6, 6, 580, 42),
            // Phòng SQL204 (Quiz 3 - 10 câu)
            (3, 5, 10, 375, 80), (3, 3, 9, 420, 74), (3, 0, 8, 465, 65), (3, 7, 8, 485, 55),
            (3, 2, 7, 525, 45),
            // Phòng MAT108 (Quiz 4 - 10 câu)
            (4, 6, 10, 360, 77), (4, 9, 9, 405, 69), (4, 1, 9, 435, 61), (4, 4, 8, 470, 51),
            (4, 10, 7, 500, 38),
            // Phòng OS209 (Quiz 5 - 10 câu)
            (5, 7, 10, 395, 71), (5, 3, 9, 425, 63), (5, 6, 8, 460, 54), (5, 8, 8, 490, 44),
            // Phòng WEB301 (Quiz 6 - 6 câu)
            (6, 2, 6, 250, 67),  (6, 1, 5, 285, 56), (6, 9, 5, 300, 47),
            // Phòng SEC308 (Quiz 7 - 6 câu)
            (7, 3, 6, 270, 62),  (7, 5, 5, 295, 53), (7, 10, 5, 315, 41),
            // Phòng CLD405 (Quiz 8 - 6 câu)
            (8, 8, 6, 240, 57),  (8, 4, 5, 275, 46), (8, 7, 5, 305, 36),
            // Phòng SWE206 (Quiz 9 - 6 câu)
            (9, 9, 6, 235, 52),  (9, 0, 6, 255, 40), (9, 5, 5, 290, 30)
        };

        var wrongChoices = new Dictionary<string, string>
        {
            ["A"] = "B", ["B"] = "C", ["C"] = "D", ["D"] = "A"
        };

        foreach (var ap in attemptPlan)
        {
            var quiz = quizzes[ap.QuizIdx];
            var orderedQuestions = quiz.Questions.OrderBy(q => q.OrderIndex).ToList();
            var safeCorrect = Math.Min(ap.CorrectCount, orderedQuestions.Count);
            var computedScore = Math.Round((decimal)safeCorrect / orderedQuestions.Count * 10m, 2);

            var attempt = new QuizAttempt
            {
                QuizId = quiz.Id,
                UserId = students[ap.StudentIdx].Id,
                Score = computedScore,
                CorrectAnswersCount = safeCorrect,
                TotalQuestions = orderedQuestions.Count,
                TimeSpentSeconds = ap.SecondsSpent,
                CompletedAt = now.AddHours(-ap.HoursAgo)
            };

            for (var qIdx = 0; qIdx < orderedQuestions.Count; qIdx++)
            {
                var q = orderedQuestions[qIdx];
                var makeCorrect = qIdx < safeCorrect;
                var correctOpt = q.CorrectOption.Trim().ToUpperInvariant();
                var chosenOpt = makeCorrect ? correctOpt : wrongChoices.GetValueOrDefault(correctOpt, "A");

                attempt.Answers.Add(new QuizAttemptAnswer
                {
                    QuestionId = q.Id,
                    SelectedOption = chosenOpt,
                    IsCorrect = makeCorrect
                });
            }

            context.QuizAttempts.Add(attempt);
        }

        // 8. Lưu 6 Bộ thẻ FlashcardDecks (60 thẻ học 3D)
        var decks = VkuQuizAndChatSeedCatalog.BuildSixFlashcardDecks(rooms, courseMaterials, students, now);
        context.FlashcardDecks.AddRange(decks);

        // 9. Lưu 78 Bản ghi AiUsageLogs rải đều xuyên suốt 14 ngày gần nhất cho biểu đồ Admin Chart.js
        var featureCycle = new[]
        {
            AiFeatureType.BotChat,
            AiFeatureType.ExtractPdf,
            AiFeatureType.GenerateQuiz,
            AiFeatureType.BotChat,
            AiFeatureType.GenerateFlashcards,
            AiFeatureType.BotChat
        };

        for (var dayOffset = 13; dayOffset >= 0; dayOffset--)
        {
            var callsOnDay = 4 + ((13 - dayOffset) % 4); // 4 đến 7 lượt gọi mỗi ngày -> tổng ~77 bản ghi
            for (var c = 0; c < callsOnDay; c++)
            {
                var feat = featureCycle[(dayOffset + c) % featureCycle.Length];
                var promptTokens = feat switch
                {
                    AiFeatureType.ExtractPdf => 2100 + (c * 190),
                    AiFeatureType.GenerateQuiz => 1750 + (c * 140),
                    AiFeatureType.GenerateFlashcards => 1480 + (c * 120),
                    _ => 920 + (c * 115)
                };
                var completionTokens = feat switch
                {
                    AiFeatureType.GenerateQuiz => 980 + (c * 85),
                    AiFeatureType.GenerateFlashcards => 680 + (c * 60),
                    AiFeatureType.ExtractPdf => 540 + (c * 50),
                    _ => 460 + (c * 65)
                };

                context.AiUsageLogs.Add(new AiUsageLog
                {
                    UserId = students[(dayOffset + c) % students.Count].Id,
                    RoomId = rooms[(dayOffset + c) % 10].Id,
                    FeatureType = feat,
                    ModelName = "gemini-2.5-flash",
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    TotalTokens = promptTokens + completionTokens,
                    DurationMs = 1180 + (c * 240) + (dayOffset * 35),
                    IsSuccess = !(dayOffset == 8 && c == 3),
                    CreatedAt = now.Date.AddDays(-dayOffset).AddHours(8 + c * 2).AddMinutes(15)
                });
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Hoàn tất bơm nền tảng 11 phòng VKU đầu tiên ({MsgCount} tin nhắn). Tiếp tục nạp gói mở rộng lên 22 phòng...", chatMessages.Count);

        await EnsureExpanded22RoomsAdditiveAsync(context, passwordHasher, materialsDir, logger);
        await EnsureMindmapAndReactionsBackfillAsync(context, logger);
    }

    /// <summary>
    /// Bơm cộng dồn (Idempotent & Non-Destructive) thêm 9 sinh viên VKU (23IT012..23IT020) và 11 phòng học chuyên ngành mới
    /// để nâng tổng quy mô lên 22 phòng học (20 Active + 2 Pending), tuyệt đối KHÔNG xóa tài khoản thật do người dùng tự đăng ký.
    /// </summary>
    private static async Task EnsureExpanded22RoomsAdditiveAsync(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        string materialsDir,
        ILogger logger)
    {
        var expandedSpecs = VkuExpandedCatalog.GetExpandedRooms();
        var now = DateTime.UtcNow;

        // 1. Luôn đảm bảo các tệp PDF vật lý của 11 phòng mới tồn tại đầy đủ trên ổ đĩa
        var pdfSizes = new Dictionary<string, long>();
        foreach (var spec in expandedSpecs)
        {
            var size = SamplePdfGenerator.EnsureMultiPagePdfExists(
                materialsDir,
                new SamplePdfGenerator.PdfSeedSpec(spec.PdfFileName, spec.InviteCode, spec.PdfTitle, spec.PdfSections));
            pdfSizes[spec.PdfFileName] = size;
        }

        // Kiểm tra xem đã bơm đủ các phòng mở rộng chưa
        var alreadyHasAllExpanded = await context.StudyRooms.CountAsync() >= 22;
        if (alreadyHasAllExpanded)
        {
            return;
        }

        logger.LogInformation("Đang bơm cộng dồn gói dữ liệu phong phú 22 phòng học VKU (giữ nguyên toàn bộ tài khoản hiện có)...");

        // 2. Bổ sung thêm 9 sinh viên VKU (23IT012..23IT020) nếu chưa tồn tại
        var extraStudentSpecs = VkuExpandedCatalog.GetExtraStudents();
        var existingEmails = (await context.Users.Select(u => u.Email.ToLower()).ToListAsync()).ToHashSet();
        string? lazyStudentHash = null;

        var newUsers = new List<User>();
        for (var i = 0; i < extraStudentSpecs.Length; i++)
        {
            var st = extraStudentSpecs[i];
            if (existingEmails.Contains(st.Email.ToLowerInvariant()))
            {
                continue;
            }

            lazyStudentHash ??= passwordHasher.HashPassword(StudentDefaultPassword);
            newUsers.Add(new User
            {
                StudentCode = st.Code,
                FullName = st.Name,
                Email = st.Email,
                PasswordHash = lazyStudentHash,
                AvatarUrl = $"https://api.dicebear.com/7.x/avataaars/svg?seed={st.SeedName}",
                Role = UserRole.Student,
                IsLocked = false,
                CreatedAt = now.AddDays(-14 + i)
            });
        }

        if (newUsers.Count > 0)
        {
            context.Users.AddRange(newUsers);
            await context.SaveChangesAsync();
        }

        // Lấy danh sách 20 sinh viên VKU chuẩn (23IT001..23IT020) theo thứ tự mã sinh viên
        var vkuStudents = await context.Users
            .Where(u => u.Role == UserRole.Student && u.StudentCode != null && u.StudentCode.StartsWith("23IT"))
            .OrderBy(u => u.StudentCode)
            .ToListAsync();

        if (vkuStudents.Count == 0)
        {
            return;
        }

        var existingCodes = (await context.StudyRooms.Select(r => r.InviteCode.ToUpper()).ToListAsync()).ToHashSet();
        var wrongChoices = new Dictionary<string, string>
        {
            ["A"] = "B", ["B"] = "C", ["C"] = "D", ["D"] = "A"
        };

        string[] discussionReplies =
        [
            "Tài liệu PDF của phòng tóm tắt súc tích quá, đọc lướt 5 phút nắm trọn công thức cốt lõi luôn!",
            "Mình vừa ghim câu trả lời của @bot lên thanh đầu phòng rồi, anh em nhớ xem kỹ trước khi làm đề nhé.",
            "Có bạn nào bật đồng hồ Pomodoro 25 phút cày hết bộ thẻ Flashcards 3D tối nay cùng mình không?",
            "Mình vừa thử sức đề trắc nghiệm đạt 10/10, lời giải từng câu phân tích cực kỳ dễ hiểu!",
            "Nhớ bấm Ctrl+K để chuyển nhanh qua lại giữa các phòng chuyên ngành VKU siêu tiện luôn nha."
        ];

        var addedRoomCount = 0;
        for (var idx = 0; idx < expandedSpecs.Count; idx++)
        {
            var spec = expandedSpecs[idx];
            if (existingCodes.Contains(spec.InviteCode.ToUpperInvariant()))
            {
                continue;
            }

            User PickStudent(int desiredIdx) => vkuStudents[desiredIdx % vkuStudents.Count];
            var owner = PickStudent(spec.MemberStudentIndices[0]);

            // Tạo Phòng học
            var room = new StudyRoom
            {
                Name = spec.Name,
                SubjectCategory = spec.SubjectCategory,
                Description = spec.Description,
                InviteCode = spec.InviteCode,
                Status = spec.Status,
                CreatedById = owner.Id,
                CreatedAt = now.AddDays(-spec.DaysAgo)
            };
            context.StudyRooms.Add(room);
            await context.SaveChangesAsync();

            // Tạo danh sách Thành viên
            var distinctMembers = spec.MemberStudentIndices
                .Select(PickStudent)
                .GroupBy(u => u.Id)
                .Select(g => g.First())
                .ToList();

            for (var mPos = 0; mPos < distinctMembers.Count; mPos++)
            {
                context.RoomMembers.Add(new RoomMember
                {
                    RoomId = room.Id,
                    UserId = distinctMembers[mPos].Id,
                    RoleInRoom = mPos == 0 ? RoomMemberRole.Owner : RoomMemberRole.Member,
                    JoinedAt = room.CreatedAt.AddHours(mPos * 2)
                });
            }

            // Tạo Giáo trình PDF
            var material = new CourseMaterial
            {
                RoomId = room.Id,
                UploadedById = owner.Id,
                Title = spec.PdfTitle,
                OriginalFileName = spec.PdfFileName,
                StoredFilePath = $"/uploads/materials/{spec.PdfFileName}",
                FileSizeBytes = pdfSizes.GetValueOrDefault(spec.PdfFileName, 2600),
                PageCount = spec.LogicalPages,
                ExtractedText = string.Join("\n\n", spec.PdfSections),
                AiSummary = spec.AiSummary,
                IsApproved = true,
                CreatedAt = room.CreatedAt.AddHours(1)
            };
            context.CourseMaterials.Add(material);
            await context.SaveChangesAsync();

            // Tạo Lịch sử Hội thoại & Phản hồi @bot Đã Ghim
            var baseMinutesAgo = 220 - (idx * 12);
            context.ChatMessages.Add(new ChatMessage
            {
                RoomId = room.Id,
                SenderId = owner.Id,
                IsAiBot = false,
                IsPinned = false,
                Content = $"Chào mừng cả nhóm vào phòng #{spec.InviteCode}! Mình đã tải giáo trình \"{spec.PdfTitle}\" lên cột bên phải rồi nhé.",
                CreatedAt = now.AddMinutes(-baseMinutesAgo)
            });

            var questioner = distinctMembers[Math.Min(1, distinctMembers.Count - 1)];
            context.ChatMessages.Add(new ChatMessage
            {
                RoomId = room.Id,
                SenderId = questioner.Id,
                IsAiBot = false,
                IsPinned = false,
                Content = spec.BotQuestion,
                CreatedAt = now.AddMinutes(-(baseMinutesAgo - 4))
            });

            context.ChatMessages.Add(new ChatMessage
            {
                RoomId = room.Id,
                SenderId = null,
                IsAiBot = true,
                IsPinned = true,
                Content = spec.BotPinnedMarkdown,
                CreatedAt = now.AddMinutes(-(baseMinutesAgo - 5))
            });

            for (var d = 0; d < discussionReplies.Length; d++)
            {
                var speaker = distinctMembers[(d + 1) % distinctMembers.Count];
                context.ChatMessages.Add(new ChatMessage
                {
                    RoomId = room.Id,
                    SenderId = speaker.Id,
                    IsAiBot = false,
                    IsPinned = false,
                    Content = discussionReplies[d],
                    CreatedAt = now.AddMinutes(-(baseMinutesAgo - 12 - d * 9))
                });
            }

            // Tạo Đề thi Quiz
            var quiz = new Quiz
            {
                RoomId = room.Id,
                MaterialId = material.Id,
                CreatedById = owner.Id,
                Title = spec.QuizTitle,
                QuestionCount = spec.Questions.Length,
                DurationMinutes = 10,
                Difficulty = spec.Difficulty,
                CreatedAt = room.CreatedAt.AddHours(3)
            };

            for (var qIdx = 0; qIdx < spec.Questions.Length; qIdx++)
            {
                var qt = spec.Questions[qIdx];
                quiz.Questions.Add(new QuizQuestion
                {
                    OrderIndex = qIdx + 1,
                    QuestionText = qt.Q,
                    OptionA = qt.A,
                    OptionB = qt.B,
                    OptionC = qt.C,
                    OptionD = qt.D,
                    CorrectOption = qt.Ans,
                    Explanation = qt.Exp
                });
            }
            context.Quizzes.Add(quiz);
            await context.SaveChangesAsync();

            // Tạo Lượt làm bài đua Top Leaderboard cho phòng
            var orderedQs = quiz.Questions.OrderBy(q => q.OrderIndex).ToList();
            var attemptCount = Math.Min(5, distinctMembers.Count);
            for (var a = 0; a < attemptCount; a++)
            {
                var taker = distinctMembers[a];
                var correctCnt = Math.Max(1, orderedQs.Count - (a / 2));
                var score = Math.Round((decimal)correctCnt / orderedQs.Count * 10m, 2);

                var attempt = new QuizAttempt
                {
                    QuizId = quiz.Id,
                    UserId = taker.Id,
                    Score = score,
                    CorrectAnswersCount = correctCnt,
                    TotalQuestions = orderedQs.Count,
                    TimeSpentSeconds = 195 + a * 28,
                    CompletedAt = now.AddHours(-(40 - a * 5 - idx))
                };

                for (var qIdx = 0; qIdx < orderedQs.Count; qIdx++)
                {
                    var q = orderedQs[qIdx];
                    var isOk = qIdx < correctCnt;
                    var rightOpt = q.CorrectOption.Trim().ToUpperInvariant();
                    attempt.Answers.Add(new QuizAttemptAnswer
                    {
                        QuestionId = q.Id,
                        SelectedOption = isOk ? rightOpt : wrongChoices.GetValueOrDefault(rightOpt, "A"),
                        IsCorrect = isOk
                    });
                }

                context.QuizAttempts.Add(attempt);
            }

            // Tạo Bộ thẻ 3D FlashcardDeck
            var deck = new FlashcardDeck
            {
                RoomId = room.Id,
                MaterialId = material.Id,
                CreatedById = owner.Id,
                Title = spec.DeckTitle,
                CardCount = spec.Cards.Length,
                CreatedAt = room.CreatedAt.AddHours(4)
            };
            foreach (var c in spec.Cards)
            {
                deck.Cards.Add(new Flashcard
                {
                    FrontContent = c.Front,
                    BackContent = c.Back,
                    MasteryLevel = c.Mastery
                });
            }
            context.FlashcardDecks.Add(deck);

            // Bổ sung Nhật ký Hoạt động AI rải đều cho phòng mới
            var featTypes = new[] { AiFeatureType.ExtractPdf, AiFeatureType.BotChat, AiFeatureType.GenerateQuiz, AiFeatureType.GenerateFlashcards };
            for (var f = 0; f < featTypes.Length; f++)
            {
                context.AiUsageLogs.Add(new AiUsageLog
                {
                    UserId = distinctMembers[f % distinctMembers.Count].Id,
                    RoomId = room.Id,
                    FeatureType = featTypes[f],
                    ModelName = "gemini-2.5-flash",
                    PromptTokens = 1250 + f * 210,
                    CompletionTokens = 620 + f * 95,
                    TotalTokens = 1870 + f * 305,
                    DurationMs = 1280 + f * 190,
                    IsSuccess = true,
                    CreatedAt = now.Date.AddDays(-(idx % 13)).AddHours(9 + f * 2)
                });
            }

            addedRoomCount++;
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Hoàn tất bơm cộng dồn thêm {AddedCount} phòng học mới! Tổng quy mô hiện đạt 22 phòng học (20 Active + 2 Pending) và 24 PDF.", addedRoomCount);
    }

    private static async Task EnsureMindmapAndReactionsBackfillAsync(AppDbContext context, ILogger logger)
    {
        var materialsMissingMindmap = await context.CourseMaterials
            .Where(m => m.AiMindmap == null || m.AiMindmap == "")
            .ToListAsync();

        if (materialsMissingMindmap.Count > 0)
        {
            foreach (var mat in materialsMissingMindmap)
            {
                mat.AiMindmap = BuildSeedMermaidMindmap(mat.Title, mat.AiSummary, mat.ExtractedText);
            }
            await context.SaveChangesAsync();
            logger.LogInformation("Đã bổ sung Sơ đồ Tư duy AI (Mermaid Mindmap) mẫu cho {Count} học liệu PDF.", materialsMissingMindmap.Count);
        }

        var pinnedWithoutReactions = await context.ChatMessages
            .Where(m => m.IsPinned && (m.ReactionsJson == null || m.ReactionsJson == ""))
            .ToListAsync();

        if (pinnedWithoutReactions.Count > 0)
        {
            var sampleUsers = await context.Users
                .AsNoTracking()
                .Where(u => u.Role == UserRole.Student)
                .OrderBy(u => u.Id)
                .Take(5)
                .Select(u => u.Id)
                .ToListAsync();

            if (sampleUsers.Count >= 2)
            {
                for (var idx = 0; idx < pinnedWithoutReactions.Count; idx++)
                {
                    var u1 = sampleUsers[idx % sampleUsers.Count];
                    var u2 = sampleUsers[(idx + 1) % sampleUsers.Count];
                    var u3 = sampleUsers[(idx + 2) % sampleUsers.Count];
                    pinnedWithoutReactions[idx].ReactionsJson =
                        $"{{\"🔥\":[{u1},{u2}],\"💡\":[{u2},{u3}],\"🎯\":[{u1}]}}";
                }
                await context.SaveChangesAsync();
            }
        }
    }

    private static string BuildSeedMermaidMindmap(string title, string? summary, string? extractedText)
    {
        static string CleanLabel(string? raw, int maxLen = 50)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "Kiến thức trọng tâm";
            }

            var cleaned = raw
                .Replace("\"", "'")
                .Replace("`", "")
                .Replace("[", "(")
                .Replace("]", ")")
                .Replace("{", "(")
                .Replace("}", ")")
                .Replace("<", "")
                .Replace(">", "")
                .Replace("|", "-")
                .Replace("#", "")
                .Replace("*", "")
                .Replace("$", "");

            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim(' ', '-', '•', ':', '.', ',', ';');
            if (cleaned.Length > maxLen)
            {
                cleaned = cleaned[..(maxLen - 1)].TrimEnd() + "…";
            }

            return string.IsNullOrWhiteSpace(cleaned) ? "Luận điểm học thuật" : cleaned;
        }

        var rawSource = $"{summary ?? string.Empty}\n{extractedText ?? string.Empty}";
        var bulletCandidates = rawSource
            .Split(['\n', '\r', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => CleanLabel(s, 48))
            .Where(s => s.Length >= 18 && !s.StartsWith("Tóm tắt", StringComparison.OrdinalIgnoreCase) && !s.StartsWith("---", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        while (bulletCandidates.Count < 8)
        {
            bulletCandidates.Add($"Trọng tâm học thuật #{bulletCandidates.Count + 1}");
        }

        var rootText = CleanLabel(title, 54);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("flowchart TD");
        sb.AppendLine($"    root[\"📚 {rootText}\"]");
        sb.AppendLine("    b1[\"1. Nền tảng & Khái niệm Cốt lõi\"]");
        sb.AppendLine("    b2[\"2. Cơ chế & Giải thuật Trọng tâm\"]");
        sb.AppendLine("    b3[\"3. Phân tích Hiệu năng & Kiến trúc\"]");
        sb.AppendLine("    b4[\"4. Vận dụng Thực tiễn & Ôn thi\"]");
        sb.AppendLine("    root --> b1");
        sb.AppendLine("    root --> b2");
        sb.AppendLine("    root --> b3");
        sb.AppendLine("    root --> b4");

        for (var b = 0; b < 4; b++)
        {
            var cA = bulletCandidates[b * 2];
            var cB = bulletCandidates[b * 2 + 1];
            sb.AppendLine($"    c{b + 1}_1[\"{cA}\"]");
            sb.AppendLine($"    c{b + 1}_2[\"{cB}\"]");
            sb.AppendLine($"    b{b + 1} --> c{b + 1}_1");
            sb.AppendLine($"    b{b + 1} --> c{b + 1}_2");
        }

        return sb.ToString().TrimEnd();
    }

    private static async Task EnsureSchemaUpgradesOnSqlServerAsync(AppDbContext context)
    {
        if (!context.Database.IsRelational())
        {
            return;
        }

        const string alterSql = @"
            IF COL_LENGTH('dbo.ChatMessages', 'IsPinned') IS NULL
            BEGIN
                ALTER TABLE [dbo].[ChatMessages]
                ADD [IsPinned] BIT NOT NULL CONSTRAINT [DF_ChatMessages_IsPinned] DEFAULT ((0));
            END

            IF COL_LENGTH('dbo.ChatMessages', 'ReactionsJson') IS NULL
            BEGIN
                ALTER TABLE [dbo].[ChatMessages]
                ADD [ReactionsJson] NVARCHAR(2000) NULL;
            END

            IF COL_LENGTH('dbo.CourseMaterials', 'AiMindmap') IS NULL
            BEGIN
                ALTER TABLE [dbo].[CourseMaterials]
                ADD [AiMindmap] NVARCHAR(MAX) NULL;
            END

            IF EXISTS (
                SELECT 1 FROM sys.check_constraints
                WHERE name = N'CK_AiUsageLogs_FeatureType'
                  AND (definition NOT LIKE N'%GenerateMindmap%' OR definition NOT LIKE N'%AnalyzeWeakness%')
            )
            BEGIN
                ALTER TABLE [dbo].[AiUsageLogs] DROP CONSTRAINT [CK_AiUsageLogs_FeatureType];
                ALTER TABLE [dbo].[AiUsageLogs]
                ADD CONSTRAINT [CK_AiUsageLogs_FeatureType]
                CHECK ([FeatureType] IN (N'ExtractPdf', N'BotChat', N'GenerateQuiz', N'GenerateFlashcards', N'GenerateMindmap', N'AnalyzeWeakness'));
            END

            IF OBJECT_ID(N'dbo.PasswordResetTokens', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[PasswordResetTokens] (
                    [Id]           INT            IDENTITY(1,1) NOT NULL,
                    [UserId]       INT            NOT NULL,
                    [OtpHash]      NVARCHAR(255)  NOT NULL,
                    [ExpiresAt]    DATETIME2(7)   NOT NULL,
                    [IsUsed]       BIT            NOT NULL CONSTRAINT [DF_PasswordResetTokens_IsUsed] DEFAULT ((0)),
                    [AttemptCount] INT            NOT NULL CONSTRAINT [DF_PasswordResetTokens_AttemptCount] DEFAULT ((0)),
                    [CreatedAt]    DATETIME2(7)   NOT NULL CONSTRAINT [DF_PasswordResetTokens_CreatedAt] DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT [PK_PasswordResetTokens] PRIMARY KEY CLUSTERED ([Id] ASC),
                    CONSTRAINT [FK_PasswordResetTokens_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE
                );

                CREATE NONCLUSTERED INDEX [IX_PasswordResetTokens_UserId_ExpiresAt]
                ON [dbo].[PasswordResetTokens] ([UserId] ASC, [IsUsed] ASC, [ExpiresAt] DESC);
            END";

        await context.Database.ExecuteSqlRawAsync(alterSql);
    }

    private static async Task ClearSparseDataSafelyAsync(AppDbContext context)
    {
        context.PasswordResetTokens.RemoveRange(context.PasswordResetTokens);
        context.QuizAttemptAnswers.RemoveRange(context.QuizAttemptAnswers);
        context.QuizAttempts.RemoveRange(context.QuizAttempts);
        context.QuizQuestions.RemoveRange(context.QuizQuestions);
        context.Quizzes.RemoveRange(context.Quizzes);
        context.Flashcards.RemoveRange(context.Flashcards);
        context.FlashcardDecks.RemoveRange(context.FlashcardDecks);
        context.ChatMessages.RemoveRange(context.ChatMessages);
        context.AiUsageLogs.RemoveRange(context.AiUsageLogs);
        context.CourseMaterials.RemoveRange(context.CourseMaterials);
        context.RoomMembers.RemoveRange(context.RoomMembers);
        context.StudyRooms.RemoveRange(context.StudyRooms);
        context.Users.RemoveRange(context.Users);
        await context.SaveChangesAsync();
    }
}
