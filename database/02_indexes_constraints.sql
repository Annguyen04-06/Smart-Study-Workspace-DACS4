/* =============================================================================
   TỆP: 02_indexes_constraints.sql
   HỆ THỐNG: Smart Study Workspace
   MÔ TẢ: Ràng buộc miền giá trị (CHECK Constraints) và Non-Clustered Indexes
          tối ưu hóa truy vấn tìm kiếm phòng học, lịch sử chat và thống kê AI.
   ============================================================================= */

USE [SmartStudyWorkspaceDb];
GO

-- =============================================================================
-- PHẦN 1: UNIQUE INDEXES & COMPOSITE UNIQUE INDEXES
-- =============================================================================

-- Email duy nhất trên toàn hệ thống
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_Email' AND object_id = OBJECT_ID(N'[dbo].[Users]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Users_Email]
        ON [dbo].[Users] ([Email] ASC);
END
GO

-- StudentCode duy nhất cho Sinh viên (Filtered Unique Index cho phép NULL đối với Admin)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_StudentCode' AND object_id = OBJECT_ID(N'[dbo].[Users]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Users_StudentCode]
        ON [dbo].[Users] ([StudentCode] ASC)
        WHERE ([StudentCode] IS NOT NULL);
END
GO

-- Mã mời phòng học InviteCode duy nhất để tham gia nhanh 1 chạm
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StudyRooms_InviteCode' AND object_id = OBJECT_ID(N'[dbo].[StudyRooms]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_StudyRooms_InviteCode]
        ON [dbo].[StudyRooms] ([InviteCode] ASC);
END
GO

-- Mỗi sinh viên chỉ xuất hiện tối đa 1 bản ghi thành viên trong cùng 1 phòng học
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RoomMembers_RoomId_UserId' AND object_id = OBJECT_ID(N'[dbo].[RoomMembers]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_RoomMembers_RoomId_UserId]
        ON [dbo].[RoomMembers] ([RoomId] ASC, [UserId] ASC);
END
GO

-- =============================================================================
-- PHẦN 2: NON-CLUSTERED PERFORMANCE INDEXES
-- =============================================================================

-- Tối ưu tải phân trang lịch sử tin nhắn & lấy 12 tin nhắn gần nhất (Sliding Window cho @bot)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatMessages_RoomId_CreatedAt' AND object_id = OBJECT_ID(N'[dbo].[ChatMessages]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ChatMessages_RoomId_CreatedAt]
        ON [dbo].[ChatMessages] ([RoomId] ASC, [CreatedAt] DESC)
        INCLUDE ([SenderId], [IsAiBot], [IsPinned]);
END
GO

-- Tối ưu lọc & tìm kiếm phòng học theo môn học và trạng thái trên Dashboard
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StudyRooms_SubjectCategory_Status' AND object_id = OBJECT_ID(N'[dbo].[StudyRooms]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_StudyRooms_SubjectCategory_Status]
        ON [dbo].[StudyRooms] ([Status] ASC, [SubjectCategory] ASC, [CreatedAt] DESC);
END
GO

-- Tối ưu truy vấn kho học liệu PDF đã duyệt trong từng phòng học
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CourseMaterials_RoomId_CreatedAt' AND object_id = OBJECT_ID(N'[dbo].[CourseMaterials]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CourseMaterials_RoomId_CreatedAt]
        ON [dbo].[CourseMaterials] ([RoomId] ASC, [CreatedAt] DESC);
END
GO

-- Tối ưu danh sách đề thi trắc nghiệm theo phòng học
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Quizzes_RoomId_CreatedAt' AND object_id = OBJECT_ID(N'[dbo].[Quizzes]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Quizzes_RoomId_CreatedAt]
        ON [dbo].[Quizzes] ([RoomId] ASC, [CreatedAt] DESC);
END
GO

-- Tối ưu sắp xếp thứ tự câu hỏi trong đề thi
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QuizQuestions_QuizId_OrderIndex' AND object_id = OBJECT_ID(N'[dbo].[QuizQuestions]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_QuizQuestions_QuizId_OrderIndex]
        ON [dbo].[QuizQuestions] ([QuizId] ASC, [OrderIndex] ASC);
END
GO

-- Tối ưu trang Hồ sơ Sinh viên xem lại lịch sử điểm thi cá nhân
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QuizAttempts_UserId_CompletedAt' AND object_id = OBJECT_ID(N'[dbo].[QuizAttempts]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_QuizAttempts_UserId_CompletedAt]
        ON [dbo].[QuizAttempts] ([UserId] ASC, [CompletedAt] DESC);
END
GO

-- Tối ưu danh sách bộ thẻ ghi nhớ theo phòng học
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FlashcardDecks_RoomId_CreatedAt' AND object_id = OBJECT_ID(N'[dbo].[FlashcardDecks]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_FlashcardDecks_RoomId_CreatedAt]
        ON [dbo].[FlashcardDecks] ([RoomId] ASC, [CreatedAt] DESC);
END
GO

-- Tối ưu biểu đồ thống kê Token 7 ngày & phân loại tính năng AI trên Admin Portal
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AiUsageLogs_CreatedAt_FeatureType' AND object_id = OBJECT_ID(N'[dbo].[AiUsageLogs]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AiUsageLogs_CreatedAt_FeatureType]
        ON [dbo].[AiUsageLogs] ([CreatedAt] DESC, [FeatureType] ASC)
        INCLUDE ([TotalTokens], [PromptTokens], [CompletionTokens], [IsSuccess]);
END
GO

-- Tối ưu đối chiếu mã OTP khôi phục mật khẩu còn hạn theo UserId
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PasswordResetTokens_UserId_ExpiresAt' AND object_id = OBJECT_ID(N'[dbo].[PasswordResetTokens]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PasswordResetTokens_UserId_ExpiresAt]
        ON [dbo].[PasswordResetTokens] ([UserId] ASC, [IsUsed] ASC, [ExpiresAt] DESC);
END
GO

-- =============================================================================
-- PHẦN 3: CHECK CONSTRAINTS ĐẢM BẢO TOÀN VẸN MIỀN DỮ LIỆU
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Users_Role')
BEGIN
    ALTER TABLE [dbo].[Users]
        ADD CONSTRAINT [CK_Users_Role] CHECK ([Role] IN (N'Admin', N'Student'));
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_StudyRooms_Status')
BEGIN
    ALTER TABLE [dbo].[StudyRooms]
        ADD CONSTRAINT [CK_StudyRooms_Status] CHECK ([Status] IN (N'Pending', N'Active', N'Closed'));
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_RoomMembers_RoleInRoom')
BEGIN
    ALTER TABLE [dbo].[RoomMembers]
        ADD CONSTRAINT [CK_RoomMembers_RoleInRoom] CHECK ([RoleInRoom] IN (N'Owner', N'Member'));
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Quizzes_Difficulty')
BEGIN
    ALTER TABLE [dbo].[Quizzes]
        ADD CONSTRAINT [CK_Quizzes_Difficulty] CHECK ([Difficulty] IN (N'Easy', N'Medium', N'Hard'));
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_QuizQuestions_CorrectOption')
BEGIN
    ALTER TABLE [dbo].[QuizQuestions]
        ADD CONSTRAINT [CK_QuizQuestions_CorrectOption] CHECK ([CorrectOption] IN ('A', 'B', 'C', 'D'));
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_QuizAttemptAnswers_SelectedOption')
BEGIN
    ALTER TABLE [dbo].[QuizAttemptAnswers]
        ADD CONSTRAINT [CK_QuizAttemptAnswers_SelectedOption] CHECK ([SelectedOption] IS NULL OR [SelectedOption] IN ('A', 'B', 'C', 'D'));
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Flashcards_MasteryLevel')
BEGIN
    ALTER TABLE [dbo].[Flashcards]
        ADD CONSTRAINT [CK_Flashcards_MasteryLevel] CHECK ([MasteryLevel] IN (0, 1, 2));
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AiUsageLogs_FeatureType')
BEGIN
    ALTER TABLE [dbo].[AiUsageLogs]
        ADD CONSTRAINT [CK_AiUsageLogs_FeatureType] CHECK ([FeatureType] IN (N'ExtractPdf', N'BotChat', N'GenerateQuiz', N'GenerateFlashcards', N'GenerateMindmap', N'AnalyzeWeakness'));
END
GO
