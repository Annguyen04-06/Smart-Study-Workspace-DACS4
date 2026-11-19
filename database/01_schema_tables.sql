/* =============================================================================
   TỆP: 01_schema_tables.sql
   HỆ THỐNG: Smart Study Workspace (Nền tảng Học tập Thông minh tích hợp AI)
   MÔ TẢ: Định nghĩa lược đồ 12 bảng chuẩn 3NF trên Microsoft SQL Server.
   GHI CHÚ KIẾN TRÚC:
   - Toàn bộ cột chứa tiếng Việt sử dụng kiểu NVARCHAR.
   - Các khóa ngoại trỏ về Users và CourseMaterials từ bảng con/bảng trung gian
     sử dụng ON DELETE NO ACTION (tương ứng DeleteBehavior.Restrict / NoAction
     trong EF Core 8) nhằm triệt tiêu hoàn toàn lỗi Multiple Cascade Paths.
   ============================================================================= */

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = N'SmartStudyWorkspaceDb')
BEGIN
    CREATE DATABASE [SmartStudyWorkspaceDb];
END
GO

USE [SmartStudyWorkspaceDb];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Bảng Users: Quản lý tài khoản Quản trị viên và Sinh viên
IF OBJECT_ID(N'[dbo].[Users]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Users] (
        [Id]            INT             IDENTITY(1,1) NOT NULL,
        [StudentCode]   NVARCHAR(20)    NULL,
        [FullName]      NVARCHAR(100)   NOT NULL,
        [Email]         NVARCHAR(150)   NOT NULL,
        [PasswordHash]  NVARCHAR(255)   NOT NULL,
        [AvatarUrl]     NVARCHAR(500)   NULL,
        [Role]          NVARCHAR(20)    NOT NULL CONSTRAINT [DF_Users_Role] DEFAULT (N'Student'),
        [IsLocked]      BIT             NOT NULL CONSTRAINT [DF_Users_IsLocked] DEFAULT ((0)),
        [CreatedAt]     DATETIME2(7)    NOT NULL CONSTRAINT [DF_Users_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

-- 2. Bảng StudyRooms: Không gian phòng học nhóm theo môn học
IF OBJECT_ID(N'[dbo].[StudyRooms]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[StudyRooms] (
        [Id]              INT             IDENTITY(1,1) NOT NULL,
        [Name]            NVARCHAR(150)   NOT NULL,
        [SubjectCategory] NVARCHAR(100)   NOT NULL,
        [Description]     NVARCHAR(500)   NULL,
        [InviteCode]      VARCHAR(10)     NOT NULL,
        [Status]          NVARCHAR(20)    NOT NULL CONSTRAINT [DF_StudyRooms_Status] DEFAULT (N'Active'),
        [CreatedById]     INT             NOT NULL,
        [CreatedAt]       DATETIME2(7)    NOT NULL CONSTRAINT [DF_StudyRooms_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_StudyRooms] PRIMARY KEY CLUSTERED ([Id] ASC),
        -- DeleteBehavior.Restrict: Ngăn xóa User khi đang sở hữu phòng học
        CONSTRAINT [FK_StudyRooms_Users_CreatedById] FOREIGN KEY ([CreatedById])
            REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );
END
GO

-- 3. Bảng RoomMembers: Thành viên tham gia phòng học & vai trò trong phòng
IF OBJECT_ID(N'[dbo].[RoomMembers]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RoomMembers] (
        [Id]          INT             IDENTITY(1,1) NOT NULL,
        [RoomId]      INT             NOT NULL,
        [UserId]      INT             NOT NULL,
        [RoleInRoom]  NVARCHAR(20)    NOT NULL CONSTRAINT [DF_RoomMembers_RoleInRoom] DEFAULT (N'Member'),
        [JoinedAt]    DATETIME2(7)    NOT NULL CONSTRAINT [DF_RoomMembers_JoinedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_RoomMembers] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_RoomMembers_StudyRooms_RoomId] FOREIGN KEY ([RoomId])
            REFERENCES [dbo].[StudyRooms] ([Id]) ON DELETE CASCADE,
        -- DeleteBehavior.Restrict: Tránh xung đột Cascade với StudyRooms -> Users
        CONSTRAINT [FK_RoomMembers_Users_UserId] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );
END
GO

-- 4. Bảng ChatMessages: Lịch sử thảo luận thời gian thực & phản hồi của Trợ giảng @bot
IF OBJECT_ID(N'[dbo].[ChatMessages]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatMessages] (
        [Id]          BIGINT          IDENTITY(1,1) NOT NULL,
        [RoomId]      INT             NOT NULL,
        [SenderId]    INT             NULL,
        [IsAiBot]       BIT             NOT NULL CONSTRAINT [DF_ChatMessages_IsAiBot] DEFAULT ((0)),
        [IsPinned]      BIT             NOT NULL CONSTRAINT [DF_ChatMessages_IsPinned] DEFAULT ((0)),
        [Content]       NVARCHAR(MAX)   NOT NULL,
        [ReactionsJson] NVARCHAR(2000)  NULL,
        [CreatedAt]     DATETIME2(7)    NOT NULL CONSTRAINT [DF_ChatMessages_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_ChatMessages] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ChatMessages_StudyRooms_RoomId] FOREIGN KEY ([RoomId])
            REFERENCES [dbo].[StudyRooms] ([Id]) ON DELETE CASCADE,
        -- Nullable cho tin nhắn của @bot; Restrict đối với tin nhắn của sinh viên
        CONSTRAINT [FK_ChatMessages_Users_SenderId] FOREIGN KEY ([SenderId])
            REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );
END
GO

-- 5. Bảng CourseMaterials: Học liệu PDF và bản tóm tắt học thuật do AI trích xuất
IF OBJECT_ID(N'[dbo].[CourseMaterials]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CourseMaterials] (
        [Id]               INT             IDENTITY(1,1) NOT NULL,
        [RoomId]           INT             NOT NULL,
        [UploadedById]     INT             NOT NULL,
        [Title]            NVARCHAR(200)   NOT NULL,
        [OriginalFileName] NVARCHAR(255)   NOT NULL,
        [StoredFilePath]   NVARCHAR(500)   NOT NULL,
        [FileSizeBytes]    BIGINT          NOT NULL,
        [PageCount]        INT             NOT NULL CONSTRAINT [DF_CourseMaterials_PageCount] DEFAULT ((1)),
        [ExtractedText]    NVARCHAR(MAX)   NULL,
        [AiSummary]        NVARCHAR(MAX)   NULL,
        [AiMindmap]        NVARCHAR(MAX)   NULL,
        [IsApproved]       BIT             NOT NULL CONSTRAINT [DF_CourseMaterials_IsApproved] DEFAULT ((1)),
        [CreatedAt]        DATETIME2(7)    NOT NULL CONSTRAINT [DF_CourseMaterials_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_CourseMaterials] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_CourseMaterials_StudyRooms_RoomId] FOREIGN KEY ([RoomId])
            REFERENCES [dbo].[StudyRooms] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CourseMaterials_Users_UploadedById] FOREIGN KEY ([UploadedById])
            REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );
END
GO

-- 6. Bảng Quizzes: Đề thi trắc nghiệm sinh tự động từ tài liệu PDF
IF OBJECT_ID(N'[dbo].[Quizzes]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Quizzes] (
        [Id]              INT             IDENTITY(1,1) NOT NULL,
        [RoomId]          INT             NOT NULL,
        [MaterialId]      INT             NOT NULL,
        [CreatedById]     INT             NOT NULL,
        [Title]           NVARCHAR(200)   NOT NULL,
        [QuestionCount]   INT             NOT NULL,
        [DurationMinutes] INT             NOT NULL,
        [Difficulty]      NVARCHAR(20)    NOT NULL CONSTRAINT [DF_Quizzes_Difficulty] DEFAULT (N'Medium'),
        [CreatedAt]       DATETIME2(7)    NOT NULL CONSTRAINT [DF_Quizzes_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Quizzes] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Quizzes_StudyRooms_RoomId] FOREIGN KEY ([RoomId])
            REFERENCES [dbo].[StudyRooms] ([Id]) ON DELETE CASCADE,
        -- DeleteBehavior.NoAction: Tránh Multiple Cascade Paths (StudyRooms -> CourseMaterials -> Quizzes)
        CONSTRAINT [FK_Quizzes_CourseMaterials_MaterialId] FOREIGN KEY ([MaterialId])
            REFERENCES [dbo].[CourseMaterials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Quizzes_Users_CreatedById] FOREIGN KEY ([CreatedById])
            REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );
END
GO

-- 7. Bảng QuizQuestions: Ngân hàng câu hỏi 4 đáp án kèm giải thích học thuật của AI
IF OBJECT_ID(N'[dbo].[QuizQuestions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[QuizQuestions] (
        [Id]            INT             IDENTITY(1,1) NOT NULL,
        [QuizId]        INT             NOT NULL,
        [OrderIndex]    INT             NOT NULL,
        [QuestionText]  NVARCHAR(MAX)   NOT NULL,
        [OptionA]       NVARCHAR(1000)  NOT NULL,
        [OptionB]       NVARCHAR(1000)  NOT NULL,
        [OptionC]       NVARCHAR(1000)  NOT NULL,
        [OptionD]       NVARCHAR(1000)  NOT NULL,
        [CorrectOption] CHAR(1)         NOT NULL,
        [Explanation]   NVARCHAR(MAX)   NOT NULL,
        CONSTRAINT [PK_QuizQuestions] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_QuizQuestions_Quizzes_QuizId] FOREIGN KEY ([QuizId])
            REFERENCES [dbo].[Quizzes] ([Id]) ON DELETE CASCADE
    );
END
GO

-- 8. Bảng QuizAttempts: Lịch sử các lần làm bài kiểm tra của sinh viên
IF OBJECT_ID(N'[dbo].[QuizAttempts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[QuizAttempts] (
        [Id]                  INT             IDENTITY(1,1) NOT NULL,
        [QuizId]              INT             NOT NULL,
        [UserId]              INT             NOT NULL,
        [Score]               DECIMAL(5,2)    NOT NULL,
        [CorrectAnswersCount] INT             NOT NULL,
        [TotalQuestions]      INT             NOT NULL,
        [TimeSpentSeconds]    INT             NOT NULL,
        [CompletedAt]         DATETIME2(7)    NOT NULL CONSTRAINT [DF_QuizAttempts_CompletedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_QuizAttempts] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_QuizAttempts_Quizzes_QuizId] FOREIGN KEY ([QuizId])
            REFERENCES [dbo].[Quizzes] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_QuizAttempts_Users_UserId] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );
END
GO

-- 9. Bảng QuizAttemptAnswers: Chi tiết lựa chọn từng câu trong một lượt thi
IF OBJECT_ID(N'[dbo].[QuizAttemptAnswers]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[QuizAttemptAnswers] (
        [Id]             INT      IDENTITY(1,1) NOT NULL,
        [AttemptId]      INT      NOT NULL,
        [QuestionId]     INT      NOT NULL,
        [SelectedOption] CHAR(1)  NULL,
        [IsCorrect]      BIT      NOT NULL,
        CONSTRAINT [PK_QuizAttemptAnswers] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_QuizAttemptAnswers_QuizAttempts_AttemptId] FOREIGN KEY ([AttemptId])
            REFERENCES [dbo].[QuizAttempts] ([Id]) ON DELETE CASCADE,
        -- DeleteBehavior.Restrict: Tránh vòng lặp Cascade giữa QuizAttempts và QuizQuestions
        CONSTRAINT [FK_QuizAttemptAnswers_QuizQuestions_QuestionId] FOREIGN KEY ([QuestionId])
            REFERENCES [dbo].[QuizQuestions] ([Id]) ON DELETE NO ACTION
    );
END
GO

-- 10. Bảng FlashcardDecks: Bộ thẻ ghi nhớ khái niệm/công thức trích xuất từ PDF
IF OBJECT_ID(N'[dbo].[FlashcardDecks]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[FlashcardDecks] (
        [Id]          INT             IDENTITY(1,1) NOT NULL,
        [RoomId]      INT             NOT NULL,
        [MaterialId]  INT             NOT NULL,
        [CreatedById] INT             NOT NULL,
        [Title]       NVARCHAR(200)   NOT NULL,
        [CardCount]   INT             NOT NULL,
        [CreatedAt]   DATETIME2(7)    NOT NULL CONSTRAINT [DF_FlashcardDecks_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_FlashcardDecks] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_FlashcardDecks_StudyRooms_RoomId] FOREIGN KEY ([RoomId])
            REFERENCES [dbo].[StudyRooms] ([Id]) ON DELETE CASCADE,
        -- DeleteBehavior.NoAction: Tránh Multiple Cascade Paths với CourseMaterials
        CONSTRAINT [FK_FlashcardDecks_CourseMaterials_MaterialId] FOREIGN KEY ([MaterialId])
            REFERENCES [dbo].[CourseMaterials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FlashcardDecks_Users_CreatedById] FOREIGN KEY ([CreatedById])
            REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );
END
GO

-- 11. Bảng Flashcards: Thẻ học 2 mặt tích hợp mức độ ghi nhớ Spaced Repetition
IF OBJECT_ID(N'[dbo].[Flashcards]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Flashcards] (
        [Id]           INT             IDENTITY(1,1) NOT NULL,
        [DeckId]       INT             NOT NULL,
        [FrontContent] NVARCHAR(1000)  NOT NULL,
        [BackContent]  NVARCHAR(2000)  NOT NULL,
        [MasteryLevel] INT             NOT NULL CONSTRAINT [DF_Flashcards_MasteryLevel] DEFAULT ((0)),
        CONSTRAINT [PK_Flashcards] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Flashcards_FlashcardDecks_DeckId] FOREIGN KEY ([DeckId])
            REFERENCES [dbo].[FlashcardDecks] ([Id]) ON DELETE CASCADE
    );
END
GO

-- 12. Bảng AiUsageLogs: Nhật ký tiêu thụ Token Gemini AI phục vụ giám sát Quản trị
IF OBJECT_ID(N'[dbo].[AiUsageLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AiUsageLogs] (
        [Id]               INT             IDENTITY(1,1) NOT NULL,
        [UserId]           INT             NOT NULL,
        [RoomId]           INT             NULL,
        [FeatureType]      NVARCHAR(30)    NOT NULL,
        [ModelName]        NVARCHAR(50)    NOT NULL,
        [PromptTokens]     INT             NOT NULL CONSTRAINT [DF_AiUsageLogs_PromptTokens] DEFAULT ((0)),
        [CompletionTokens] INT             NOT NULL CONSTRAINT [DF_AiUsageLogs_CompletionTokens] DEFAULT ((0)),
        [TotalTokens]      INT             NOT NULL CONSTRAINT [DF_AiUsageLogs_TotalTokens] DEFAULT ((0)),
        [DurationMs]       INT             NOT NULL CONSTRAINT [DF_AiUsageLogs_DurationMs] DEFAULT ((0)),
        [IsSuccess]        BIT             NOT NULL CONSTRAINT [DF_AiUsageLogs_IsSuccess] DEFAULT ((1)),
        [CreatedAt]        DATETIME2(7)    NOT NULL CONSTRAINT [DF_AiUsageLogs_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_AiUsageLogs] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AiUsageLogs_Users_UserId] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION,
        -- Khi xóa StudyRoom, giữ lại lịch sử tiêu thụ Token cho Admin thống kê bằng ON DELETE SET NULL
        CONSTRAINT [FK_AiUsageLogs_StudyRooms_RoomId] FOREIGN KEY ([RoomId])
            REFERENCES [dbo].[StudyRooms] ([Id]) ON DELETE SET NULL
    );
END
GO

-- 13. Bảng PasswordResetTokens: Lưu mã OTP 6 chữ số đã băm bảo mật phục vụ Khôi phục Mật khẩu qua Email
IF OBJECT_ID(N'[dbo].[PasswordResetTokens]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PasswordResetTokens] (
        [Id]           INT             IDENTITY(1,1) NOT NULL,
        [UserId]       INT             NOT NULL,
        [OtpHash]      NVARCHAR(255)   NOT NULL,
        [ExpiresAt]    DATETIME2(7)    NOT NULL,
        [IsUsed]       BIT             NOT NULL CONSTRAINT [DF_PasswordResetTokens_IsUsed] DEFAULT ((0)),
        [AttemptCount] INT             NOT NULL CONSTRAINT [DF_PasswordResetTokens_AttemptCount] DEFAULT ((0)),
        [CreatedAt]    DATETIME2(7)    NOT NULL CONSTRAINT [DF_PasswordResetTokens_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_PasswordResetTokens] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PasswordResetTokens_Users] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
    );
END
GO
