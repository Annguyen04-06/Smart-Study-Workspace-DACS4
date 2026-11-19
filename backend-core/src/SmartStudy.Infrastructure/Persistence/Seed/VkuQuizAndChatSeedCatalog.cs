using SmartStudy.Domain.Entities;
using SmartStudy.Domain.Enums;

namespace SmartStudy.Infrastructure.Persistence.Seed;

/// <summary>
/// Ngân hàng dữ liệu mẫu chất lượng cao cho 6 Đề thi Trắc nghiệm (60 câu), 6 Bộ Flashcard (62 thẻ)
/// và hơn 85 tin nhắn hội thoại nhóm kèm phản hồi @bot (bảng so sánh, C#/Python code, LaTeX & Ghim kiến thức).
/// </summary>
internal static class VkuQuizAndChatSeedCatalog
{
    public static List<Quiz> BuildSixQuizzes(IReadOnlyList<StudyRoom> rooms, IReadOnlyList<CourseMaterial> materials, IReadOnlyList<User> students, DateTime now)
    {
        Quiz CreateQuiz(int roomIdx, int matIdx, int creatorIdx, string title, QuizDifficulty diff, int durationMin, (string Q, string A, string B, string C, string D, string Ans, string Exp)[] items)
        {
            var quiz = new Quiz
            {
                RoomId = rooms[roomIdx].Id,
                MaterialId = materials[matIdx].Id,
                CreatedById = students[creatorIdx].Id,
                Title = title,
                QuestionCount = items.Length,
                DurationMinutes = durationMin,
                Difficulty = diff,
                CreatedAt = now.AddDays(-6 + roomIdx * 0.5)
            };

            for (var i = 0; i < items.Length; i++)
            {
                var it = items[i];
                quiz.Questions.Add(new QuizQuestion
                {
                    OrderIndex = i + 1,
                    QuestionText = it.Q,
                    OptionA = it.A,
                    OptionB = it.B,
                    OptionC = it.C,
                    OptionD = it.D,
                    CorrectOption = it.Ans,
                    Explanation = it.Exp
                });
            }
            return quiz;
        }

        return
        [
            // Quiz 1: NET101 (10 câu)
            CreateQuiz(0, 0, 0, "Kiểm tra Chuyên đề SignalR, WebSockets & TCP/UDP", QuizDifficulty.Medium, 15,
            [
                ("Mã trạng thái HTTP nào được máy chủ trả về để xác nhận nâng cấp kết nối sang giao thức WebSocket?", "HTTP 200 OK", "HTTP 101 Switching Protocols", "HTTP 204 No Content", "HTTP 302 Found", "B", "Theo chuẩn RFC 6455, quá trình bắt tay WebSocket sử dụng HTTP Upgrade và máy chủ phản hồi mã 101 Switching Protocols."),
                ("Thứ tự ưu tiên chuyển đổi dự phòng (Transport Fallback) mặc định trong ASP.NET Core SignalR là gì?", "Long Polling -> SSE -> WebSockets", "SSE -> WebSockets -> Long Polling", "WebSockets -> Server-Sent Events -> HTTP Long Polling", "WebSockets -> gRPC -> UDP", "C", "SignalR luôn thử kết nối WebSockets song công trước, nếu thất bại sẽ lùi về Server-Sent Events và cuối cùng là HTTP Long Polling."),
                ("Vì sao trình duyệt kết nối vào SignalR Hub phải truyền chuỗi JWT qua query string ?access_token=...?", "Do JWT quá dài so với Header", "Do WebSocket API trên trình duyệt không cho phép gắn tùy chỉnh Header Authorization khi bắt tay", "Do SignalR không hỗ trợ HTTPS", "Do CORS chặn Bearer Header", "B", "Chuẩn WebSocket trên trình duyệt không mở API gán custom HTTP Header nên SignalR gửi qua access_token và đọc tại JwtBearerEvents.OnMessageReceived."),
                ("Khi chạy tác vụ ngầm bất đồng bộ (@bot) bên trong Hub sau khi phương thức Hub kết thúc, cần dùng dịch vụ nào để lấy DbContext an toàn?", "IHttpContextAccessor", "IServiceScopeFactory để tạo Scope độc lập mới", "Singleton AppDbContext", "MemoryCache", "B", "Hub có vòng đời ngắn theo từng lượt gọi RPC; dùng lại DbContext của Hub ở luồng nền sẽ gây lỗi ObjectDisposedException."),
                ("Kích thước phần tiêu đề (Header) cố định của gói tin UDP là bao nhiêu byte?", "8 bytes", "20 bytes", "32 bytes", "60 bytes", "A", "UDP Header chỉ gồm 4 trường 16-bit (Source Port, Dest Port, Length, Checksum) tổng cộng đúng 8 bytes."),
                ("Trong giao thức TCP, thông lượng cửa sổ gửi thực tế bị giới hạn bởi công thức nào?", "max(rwnd, cwnd)", "rwnd + cwnd", "min(rwnd, cwnd)", "cwnd / rwnd", "C", "Bên gửi TCP phải đồng thời tôn trọng khả năng đệm của bên nhận (rwnd) và mức chịu tải của mạng (cwnd), tức min(rwnd, cwnd)."),
                ("Khi bên gửi TCP nhận liên tiếp 3 gói tin Duplicate ACKs giống nhau trước khi hết hạn Timeout, cơ chế nào được kích hoạt?", "Slow Start lại từ cwnd = 1", "Fast Retransmit (Truyền lại nhanh)", "Đóng kết nối RST", "Chuyển sang giao thức UDP", "B", "3 Duplicate ACKs báo hiệu một phân đoạn bị thất lạc giữa chừng trong khi các gói sau vẫn tới đích, kích hoạt Fast Retransmit ngay mà không chờ RTO."),
                ("Phương thức Clients.OthersInGroup(groupName) trong SignalR Hub gửi sự kiện tới đối tượng nào?", "Tất cả kết nối trên toàn máy chủ", "Chỉ duy nhất người vừa gọi Hub", "Tất cả thành viên trong Group ngoại trừ kết nối của người gửi hiện tại", "Chỉ trưởng phòng học", "C", "OthersInGroup rất hữu ích cho tính năng Typing Indicator vì người đang gõ không cần tự nhận lại thông báo của chính mình."),
                ("Cấu trúc dữ liệu nào trong .NET phù hợp nhất để lưu trữ PresenceTracker an toàn đa luồng?", "List<int>", "Dictionary<int, string>", "ConcurrentDictionary kết hợp đồng bộ khóa khi thao tác tập hợp con", "ArrayList", "C", "Nhiều kết nối WebSocket vào/ra đồng thời trên các luồng ThreadPool nên bắt buộc dùng cấu trúc Thread-Safe như ConcurrentDictionary."),
                ("Trạng thái TIME_WAIT khi đóng kết nối TCP 4 bước kéo dài trong thời gian chuẩn là bao nhiêu?", "1 RTT", "2 * MSL (Maximum Segment Lifetime)", "60 phút", "500 mili-giây", "B", "TIME_WAIT duy trì 2*MSL nhằm đảm bảo gói ACK cuối tới được phía bên kia và mọi gói tin trễ của phiên cũ đều đã hết hạn trên mạng.")
            ]),

            // Quiz 2: DSA202 (10 câu)
            CreateQuiz(1, 2, 1, "Đánh giá Toàn diện Đồ thị: BFS, DFS, Dijkstra & Quy hoạch động", QuizDifficulty.Hard, 15,
            [
                ("Độ phức tạp thời gian của thuật toán Dijkstra cài đặt bằng Binary Min-Heap trên đồ thị danh sách kề G = (V, E) là?", "O(|V|^2)", "O((|V| + |E|) log |V|)", "O(|V| * |E|)", "O(|E| log |E| + |V|^2)", "B", "Mỗi đỉnh trích xuất khỏi Heap mất O(log V) và tối đa |E| phép giảm khóa mất O(log V), tổng thời gian là O((|V| + |E|) log |V|)."),
                ("Vì sao thuật toán Dijkstra có thể cho kết quả SAI trên đồ thị có cạnh mang trọng số âm?", "Vì Min-Heap không lưu được số âm", "Vì giả định tham lam rằng đỉnh có d[u] nhỏ nhất lấy ra đã đạt tối ưu toàn cục bị phá vỡ khi có cạnh âm phía sau", "Vì gây tràn bộ nhớ đệ quy", "Vì đồ thị có cạnh âm luôn không liên thông", "B", "Dijkstra đóng đỉnh ngay khi lấy khỏi hàng đợi ưu tiên; nếu sau đó gặp cạnh âm làm giảm chi phí đường vòng, giả định tham lam không còn đúng."),
                ("Thuật toán Bellman-Ford cần thực hiện tối đa bao nhiêu vòng lặp thư giãn toàn bộ tập cạnh E để tìm đường đi ngắn nhất (nếu không có chu trình âm)?", "|V| - 1 vòng", "|V| + 1 vòng", "log2(|V|) vòng", "|E| vòng", "A", "Một đường đi đơn ngắn nhất không chứa chu trình đi qua tối đa |V| đỉnh tức |V| - 1 cạnh, nên cần |V| - 1 vòng thư giãn."),
                ("Để kiểm tra nhanh hai đỉnh có thuộc cùng một thành phần liên thông khi cài thuật toán Kruskal tìm Cây khung nhỏ nhất, cấu trúc nào tối ưu nhất?", "Hàng đợi FIFO", "Ngăn xếp LIFO", "Disjoint Set Union (DSU / Union-Find) có nén đường đi", "Bảng băm chuỗi", "C", "DSU với Path Compression và Union by Rank thực hiện thao tác Find/Union trong thời gian khấu hao gần hằng số O(alpha(V))."),
                ("Sắp xếp Tô-pô (Topological Sort) chỉ tồn tại trên loại đồ thị nào dưới đây?", "Đồ thị vô hướng đầy đủ", "Đồ thị có hướng không có chu trình (DAG)", "Đồ thị có chu trình trọng số dương", "Cây nhị phân tìm kiếm", "B", "Nếu đồ thị có hướng chứa chu trình thì tồn tại phụ thuộc vòng nên không thể xếp thứ tự tuyến tính thỏa mọi cung (u -> v)."),
                ("Khi tối ưu không gian bài toán Cái túi 0/1 (0/1 Knapsack) từ mảng 2 chiều xuống mảng 1 chiều dp[w], thứ tự duyệt trọng lượng w phải như thế nào?", "Duyệt tăng dần từ weight[i] đến W", "Duyệt giảm dần từ W về weight[i]", "Chỉ duyệt các số chẵn", "Duyệt ngẫu nhiên", "B", "Duyệt ngược từ W về weight[i] đảm bảo dp[w - weight[i]] vẫn là giá trị của dòng i - 1 (mỗi vật chỉ chọn tối đa 1 lần); nếu duyệt xuôi sẽ thành Unbounded Knapsack."),
                ("Cho hai xâu ký tự có độ dài m và n. Quy hoạch động tìm Dãy con chung dài nhất (LCS) tiêu tốn độ phức tạp thời gian chuẩn là?", "O(m + n)", "O(m * n)", "O(2^(m+n))", "O(n log m)", "B", "Bảng quy hoạch động dp[0..m][0..n] gồm (m+1)*(n+1) trạng thái, mỗi ô tính trong O(1), do đó tổng thời gian là O(m * n)."),
                ("Cấu trúc dữ liệu nào được dùng trực tiếp trong thuật toán duyệt theo chiều rộng (BFS)?", "Queue (Hàng đợi FIFO)", "Stack (Ngăn xếp LIFO)", "AVL Tree", "Trie", "A", "BFS duyệt theo từng lớp khoảng cách từ đỉnh nguồn nhờ cơ chế vào trước ra trước của Queue."),
                ("Với đồ thị thưa có |V| = 100.000 đỉnh và |E| = 250.000 cạnh, cách biểu diễn nào tránh tràn bộ nhớ RAM?", "Ma trận kề (Adjacency Matrix)", "Danh sách kề (Adjacency List)", "Ma trận liên thuộc (Incidence Matrix)", "Ma trận khoảng cách Floyd-Warshall", "B", "Ma trận kề cần 10^10 phần tử (~vài chục GB RAM), trong khi Danh sách kề chỉ tốn O(|V| + |E|) khoảng vài MB bộ nhớ."),
                ("Hai tính chất cốt lõi bắt buộc phải có để áp dụng hiệu quả phương pháp Quy hoạch động là gì?", "Chia để trị và Tham lam", "Bài toán con giao nhau (Overlapping Subproblems) và Cấu trúc con tối ưu (Optimal Substructure)", "Đệ quy đuôi và Quay lui", "Đồ thị phẳng và Liên thông", "B", "Quy hoạch động lưu lại lời giải các bài toán con lặp đi lặp lại và ghép chúng theo tính chất cấu trúc con tối ưu.")
            ]),

            // Quiz 3: AI305 (10 câu)
            CreateQuiz(2, 4, 4, "Trắc nghiệm Trí tuệ Nhân tạo: Tìm kiếm A*, Bayes & Gradient Descent", QuizDifficulty.Medium, 15,
            [
                ("Trong giải thuật tìm kiếm A*, hàm đánh giá tại mỗi nút n được xác định bởi công thức nào?", "f(n) = g(n) - h(n)", "f(n) = g(n) + h(n)", "f(n) = h(n) / g(n)", "f(n) = max(g(n), h(n))", "B", "f(n) tổng hợp chi phí thực tế từ điểm xuất phát đến n là g(n) cộng với chi phí ước lượng từ n tới đích là h(n)."),
                ("Điều kiện để hàm Heuristic h(n) được gọi là Chấp nhận được (Admissible) nhằm đảm bảo A* tìm ra đường đi tối ưu là?", "h(n) luôn lớn hơn chi phí thực tế h*(n)", "0 <= h(n) <= h*(n) với mọi nút n", "h(n) luôn bằng hằng số 100", "h(n) âm với mọi nút", "B", "Hàm heuristic không bao giờ được đánh giá quá cao (overestimate) chi phí thực tế h*(n) đi tới đích."),
                ("Nếu đặt h(n) = 0 với mọi trạng thái n, thuật toán A* sẽ suy biến thành thuật toán nào?", "Tìm kiếm theo chiều sâu (DFS)", "Tìm kiếm chi phí đồng nhất (Uniform Cost Search / Dijkstra)", "Tìm kiếm leo đồi (Hill Climbing)", "Tìm kiếm tham lam (Greedy Best-First)", "B", "Khi h(n) = 0, f(n) = g(n) nên A* mở rộng nút có chi phí đường đi từ gốc nhỏ nhất, chính là Uniform Cost Search / Dijkstra."),
                ("Giả định 'Ngây thơ' (Naive) trong bộ phân loại Naive Bayes là gì?", "Mọi lớp đều có xác suất bằng nhau", "Các đặc trưng đầu vào độc lập có điều kiện với nhau khi đã biết nhãn lớp", "Dữ liệu luôn phân phối chuẩn 1 chiều", "Không tồn tại nhiễu trong tập huấn luyện", "B", "Naive Bayes giả sử P(x1, x2, ..., xd | Y) = tích các P(xi | Y), giúp giảm mạnh số tham số cần ước lượng."),
                ("Kỹ thuật Làm mịn Laplace (Laplace Smoothing) trong phân loại văn bản Naive Bayes dùng để khắc phục vấn đề gì?", "Tăng tốc độ nhân ma trận GPU", "Ngăn xác suất tích bằng 0 khi gặp một từ mới chưa từng xuất hiện ở lớp đó trong tập huấn luyện", "Giảm số chiều không gian vector", "Loại bỏ từ dừng tự động", "B", "Nếu một từ có tần suất 0 trong lớp c thì P(w|c) = 0 kéo toàn bộ tích xác suất về 0; cộng thêm hệ số alpha = 1 giải quyết triệt để lỗi này."),
                ("Trong thuật toán Gradient Descent, điều gì xảy ra nếu chọn tốc độ học (Learning Rate alpha) quá lớn?", "Thuật toán luôn hội tụ về cực tiểu toàn cục nhanh gấp đôi", "Hàm mất mát có thể dao động mạnh qua lại hai bên thung lũng và phân kỳ (không hội tụ)", "Đạo hàm tự động bằng 0", "Mô hình không cần chuẩn hóa dữ liệu", "B", "Bước nhảy quá lớn làm tham số vượt quá điểm cực tiểu và thậm chí làm giá trị hàm mất mát J(theta) tăng vọt ra vô cùng."),
                ("Hiện tượng mô hình đạt độ chính xác 99.5% trên tập Huấn luyện (Train) nhưng chỉ đạt 61% trên tập Kiểm thử (Test) gọi là gì?", "Underfitting (Chưa khớp)", "Overfitting (Quá khớp)", "Vanishing Gradient", "Data Normalization", "B", "Mô hình học thuộc lòng cả nhiễu của tập Train dẫn tới khả năng tổng quát hóa kém trên dữ liệu mới (Overfitting)."),
                ("Hàm kích hoạt nào thường dùng ở tầng đầu ra cho bài toán Phân loại Nhị phân (Logistic Regression) để ép giá trị về khoảng (0, 1)?", "Hàm ReLU", "Hàm Sigmoid: 1 / (1 + e^(-z))", "Hàm tuyến tính y = z", "Hàm Step bậc thang", "B", "Hàm Sigmoid ánh xạ mọi số thực z sang khoảng xác suất liên tục (0, 1) và có đạo hàm trơn thuận tiện cho Gradient Descent."),
                ("Thành phần phạt L2 Regularization (Ridge) cộng thêm đại lượng nào vào hàm mất mát?", "Tổng trị tuyệt đối các trọng số", "Lambda nhân tổng bình phương các trọng số", "Logarit của số mẫu huấn luyện", "Độ lệch chuẩn của nhãn đầu ra", "B", "L2 Regularization cộng thêm lambda * sum(w_j^2), ép các trọng số giữ giá trị nhỏ đồng đều để giảm phương sai mô hình."),
                ("Khoảng cách Manhattan trên lưới ô vuông 4 hướng (lên, xuống, trái, phải) có công thức là?", "sqrt((x1 - x2)^2 + (y1 - y2)^2)", "|x1 - x2| + |y1 - y2|", "max(|x1 - x2|, |y1 - y2|)", "(x1 + x2) / 2", "B", "Khi chỉ di chuyển ngang và dọc trên lưới, tổng khoảng cách tuyệt đối theo hai trục là heuristic chấp nhận được chuẩn xác nhất.")
            ]),

            // Quiz 4: SQL204 (10 câu)
            CreateQuiz(3, 5, 5, "Tối ưu Truy vấn SQL Server, B+Tree Index & Giao dịch ACID", QuizDifficulty.Medium, 12,
            [
                ("Một bảng vật lý trong Microsoft SQL Server có thể sở hữu tối đa bao nhiêu Clustered Index?", "Không giới hạn", "Tối đa 1 Clustered Index duy nhất", "Tối đa 16 Clustered Index", "Tối đa 999 Clustered Index", "B", "Nút lá của Clustered Index chính là các trang dữ liệu vật lý của bảng nên dữ liệu chỉ có thể sắp xếp vật lý theo 1 thứ tự duy nhất."),
                ("Mệnh đề INCLUDE (ColumnA, ColumnB) khi tạo Non-Clustered Index giúp tối ưu truy vấn bằng cơ chế nào?", "Nén dung lượng ổ cứng xuống 10 lần", "Lưu sẵn giá trị cột phụ tại tầng lá (Covering Index) để loại bỏ hoàn toàn bước Key Lookup quay lại bảng gốc", "Tự động tạo khóa ngoại", "Chuyển bảng sang bộ nhớ RAM", "B", "Khi mọi cột trong SELECT đều nằm trong khóa chỉ mục hoặc INCLUDE, SQL Engine chỉ cần đọc Index Seek mà không tốn I/O cho Key Lookup."),
                ("Câu truy vấn nào dưới đây vi phạm nguyên tắc SARGable khiến SQL Server không thể tận dụng hiệu quả Index trên cột CreatedAt?", "WHERE CreatedAt >= '2026-01-01' AND CreatedAt < '2027-01-01'", "WHERE YEAR(CreatedAt) = 2026", "WHERE CreatedAt > SYSUTCDATETIME()", "WHERE CreatedAt BETWEEN '2026-01-01' AND '2026-12-31'", "B", "Bọc hàm YEAR() quanh cột khiến Engine phải tính giá trị hàm trên từng dòng (Index Scan) thay vì nhảy trực tiếp vào nhánh cây B+Tree (Index Seek)."),
                ("Trong 4 tính chất ACID của giao dịch CSDL, chữ 'D' (Durability - Tính bền vững) được SQL Server đảm bảo chủ yếu nhờ cơ chế nào?", "Bộ nhớ đệm truy vấn tạm", "Write-Ahead Logging (WAL) ghi nhật ký giao dịch xuống đĩa trước khi xác nhận Commit", "Khóa bảng toàn cục", "Trigger tự động", "B", "Nhờ WAL ghi trước vào Transaction Log (.ldf), ngay cả khi mất điện đột ngột sau khi Commit, SQL Server vẫn phục hồi đúng trạng thái khi khởi động lại."),
                ("Mức cô lập giao dịch (Isolation Level) mặc định của Microsoft SQL Server là gì?", "Read Uncommitted", "Read Committed", "Repeatable Read", "Serializable", "B", "Read Committed ngăn chặn hiện tượng Đọc bẩn (Dirty Read) bằng cách không đọc dữ liệu chưa Commit của giao dịch khác."),
                ("Vì sao cần thiết lập ON DELETE NO ACTION (DeleteBehavior.Restrict) trên một số khóa ngoại trỏ về bảng Users trong SQL Server?", "Vì SQL Server không hỗ trợ khóa ngoại", "Để tránh lỗi Multiple Cascade Paths khi một bảng con có thể bị xóa dây chuyền qua nhiều đường khác nhau từ cùng một bảng cha", "Để tăng kích thước cột NVARCHAR", "Để tắt kiểm tra toàn vẹn tham chiếu", "B", "SQL Server nghiêm cấm đồ thị xóa dây chuyền hội tụ (diamond cascade paths) nhằm đảm bảo thứ tự xóa nhất quán."),
                ("Một bảng đạt dạng chuẩn 3 (3NF) khi bảng đó đã đạt 2NF và thỏa mãn thêm điều kiện nào?", "Không có khóa chính ghép", "Mọi thuộc tính không khóa không phụ thuộc bắc cầu vào khóa chính", "Có ít hơn 10 cột", "Chỉ chứa kiểu dữ liệu số nguyên", "B", "Loại bỏ phụ thuộc bắc cầu (A -> B -> C) giúp triệt tiêu dư thừa dữ liệu và dị thường cập nhật."),
                ("Kích thước chuẩn của một trang dữ liệu (Data Page) cơ sở trong SQL Server Storage Engine là bao nhiêu?", "4 KB", "8 KB", "64 MB", "1 MB", "B", "SQL Server quản lý I/O theo từng trang 8 KB (8192 bytes), và 8 trang liên tiếp tạo thành 1 Extent (64 KB)."),
                ("Để lưu trữ chính xác chuỗi tiếng Việt có dấu chuẩn Unicode trong SQL Server, cần sử dụng kiểu dữ liệu nào và tiền tố hằng chuỗi nào?", "VARCHAR kèm tiền tố 'abc'", "NVARCHAR kèm tiền tố N'abc'", "CHAR kèm tiền tố U'abc'", "TEXT thuần ASCII", "B", "NVARCHAR lưu Unicode UTF-16 (2 bytes/ký tự) và hằng chuỗi tiếng Việt trong T-SQL cần tiền tố N'...' để không bị mất dấu thành dấu hỏi."),
                ("Composite Index được đánh theo thứ tự (RoomId ASC, CreatedAt DESC) phát huy hiệu quả cao nhất cho truy vấn nào?", "WHERE CreatedAt > '2026-01-01' (không lọc RoomId)", "WHERE RoomId = 1 ORDER BY CreatedAt DESC", "ORDER BY SenderId DESC", "WHERE Content LIKE N'%abc%'", "B", "Chỉ mục ghép tuân theo quy tắc tiền tố trái (Leftmost Prefix): lọc bằng cột dẫn đầu RoomId và sắp xếp sẵn theo CreatedAt DESC không tốn bước Sort.")
            ]),

            // Quiz 5: MAT108 (10 câu)
            CreateQuiz(4, 6, 6, "Ôn tập Toán Rời rạc: Tổ hợp, Hệ thức Truy hồi & Lý thuyết Đồ thị", QuizDifficulty.Easy, 12,
            [
                ("Trong một lớp học CNTT VKU có 400 sinh viên, theo Nguyên lý Dirichlet (Chuồng bồ câu), có ít nhất bao nhiêu bạn có cùng ngày sinh nhật trong năm (tính cả năm nhuận 366 ngày)?", "1 sinh viên", "2 sinh viên", "5 sinh viên", "12 sinh viên", "B", "Ta có ceil(400 / 366) = 2, do đó chắc chắn tồn tại ít nhất 2 sinh viên trùng ngày và tháng sinh."),
                ("Cho |A| = 30, |B| = 25 và |A giao B| = 10. Theo nguyên lý Bao hàm - Loại trừ, lực lượng |A hợp B| bằng bao nhiêu?", "55", "45", "35", "15", "B", "|A hợp B| = |A| + |B| - |A giao B| = 30 + 25 - 10 = 45."),
                ("Điều kiện cần và đủ để một đồ thị vô hướng liên thông (có ít nhất 1 cạnh) tồn tại Chu trình Euler là gì?", "Đồ thị có số đỉnh chẵn", "Tất cả các đỉnh của đồ thị đều có bậc chẵn", "Đồ thị có đúng 2 đỉnh bậc lẻ", "Đồ thị không có chu trình", "B", "Định lý Euler khẳng định đồ thị vô hướng liên thông có chu trình đi qua mọi cạnh đúng 1 lần khi và chỉ khi mọi đỉnh đều bậc chẵn."),
                ("Áp dụng Định lý Master cho hệ thức Chia để trị của thuật toán Merge Sort: T(n) = 2*T(n/2) + Theta(n). Độ phức tạp tiệm cận của T(n) là?", "Theta(n)", "Theta(n log n)", "Theta(n^2)", "Theta(log n)", "B", "Ở đây a = 2, b = 2 => n^(log_b a) = n^1 = n, khớp bậc với f(n) = Theta(n) (Trường hợp 2 của Master Theorem), suy ra T(n) = Theta(n log n)."),
                ("Tổng số bậc của tất cả các đỉnh trong một đồ thị vô hướng G = (V, E) luôn bằng bao nhiêu?", "|E|", "2 * |E|", "|V| * |E|", "|V| - 1", "B", "Theo Bổ đề Bắt tay (Handshaking Lemma), mỗi cạnh vô hướng đóng góp 1 đơn vị bậc cho mỗi đầu mút của nó, nên tổng bậc bằng 2|E|."),
                ("Một cây (Tree) vô hướng có n = 15 đỉnh thì có chính xác bao nhiêu cạnh?", "15 cạnh", "14 cạnh", "16 cạnh", "105 cạnh", "B", "Mọi cây liên thông không chu trình với n đỉnh luôn có đúng n - 1 = 14 cạnh."),
                ("Số tập hợp con của một tập hợp hữu hạn có n = 6 phần tử phân biệt là bao nhiêu?", "12", "36", "64", "720", "C", "Mỗi phần tử có 2 lựa chọn (xuất hiện hoặc không xuất hiện trong tập con), nên lực lượng tập lũy thừa P(A) là 2^6 = 64."),
                ("Phương trình đặc trưng của hệ thức truy hồi Fibonacci F(n) = F(n-1) + F(n-2) là phương trình nào?", "r^2 + r + 1 = 0", "r^2 - r - 1 = 0", "r^2 - 2r + 1 = 0", "r - 2 = 0", "B", "Chuyển vế F(n) - F(n-1) - F(n-2) = 0 ta thu được phương trình đặc trưng r^2 - r - 1 = 0 có nghiệm tỷ lệ vàng."),
                ("Đồ thị đầy đủ K_5 (gồm 5 đỉnh đôi một nối với nhau) có tổng cộng bao nhiêu cạnh?", "5 cạnh", "10 cạnh", "15 cạnh", "20 cạnh", "B", "Số cạnh của đồ thị đầy đủ K_n là tổ hợp chập 2 của n: n*(n-1)/2 = 5*4/2 = 10 cạnh."),
                ("Mệnh đề kéo theo P -> Q chỉ nhận giá trị chân lý SAI (False) trong trường hợp nào?", "P Đúng và Q Đúng", "P Đúng và Q Sai", "P Sai và Q Đúng", "P Sai và Q Sai", "B", "Phép kéo theo logic P -> Q tương đương (!P hoặc Q), chỉ sai duy nhất khi giả thiết P đúng mà kết luận Q sai.")
            ]),

            // Quiz 6: OS209 (10 câu)
            CreateQuiz(5, 7, 7, "Kiểm tra Nguyên lý Hệ điều hành Linux, Lập lịch CPU & Deadlock", QuizDifficulty.Medium, 15,
            [
                ("Kỹ thuật nào giúp lời gọi hệ thống fork() trên nhân Linux tạo tiến trình con cực nhanh mà không cần sao chép toàn bộ RAM ngay lập tức?", "Virtual Swapping", "Copy-on-Write (CoW)", "Direct Memory Access", "Paging Segmentation", "B", "Với Copy-on-Write, tiến trình cha và con dùng chung các trang vật lý ở chế độ chỉ đọc và chỉ nhân bản trang nào phát sinh thao tác ghi."),
                ("Bộ lập lịch Completely Fair Scheduler (CFS) trong nhân Linux lưu trữ các tiến trình sẵn sàng chạy bằng cấu trúc dữ liệu nào?", "Danh sách liên kết đơn", "Cây tìm kiếm nhị phân tự cân bằng Đỏ-Đen (Red-Black Tree) theo khóa vruntime", "Mảng tĩnh 1 chiều", "Ngăn xếp LIFO", "B", "Cây Đỏ-Đen giúp CFS tìm nút trái nhất (tiến trình có vruntime nhỏ nhất bị thiệt thời gian CPU nhất) cực nhanh và chèn/xóa trong O(log N)."),
                ("Điều kiện nào dưới đây KHÔNG nằm trong 4 điều kiện Coffman gây ra tình trạng Deadlock?", "Mutual Exclusion (Loại trừ tương hỗ)", "Hold and Wait (Giữ và Chờ)", "Preemptive Time-Sharing (Cho phép trưng dụng CPU)", "Circular Wait (Chờ đợi vòng tròn)", "C", "Điều kiện thứ 3 của Coffman là No Preemption (Không trưng dụng tài nguyên); nếu cho phép thu hồi tài nguyên thì đã phá vỡ Deadlock."),
                ("Giải thuật lập lịch CPU nào đảm bảo thời gian chờ trung bình (Average Waiting Time) là nhỏ nhất đối với tập tiến trình biết trước thời gian xử lý?", "FCFS (First-Come, First-Served)", "SJF / SRTF (Shortest Job First / Shortest Remaining Time First)", "Round Robin với quantum cực lớn", "Random Scheduling", "B", "Ưu tiên thực thi công việc ngắn nhất trước giúp giảm tổng thời gian các tiến trình phía sau phải xếp hàng chờ."),
                ("Trong giải thuật lập lịch Round Robin, nếu chọn lát thời gian (Time Quantum q) quá nhỏ tiệm cận 0 sẽ gây ra hệ quả gì?", "Tăng hiệu suất sử dụng CPU lên 100%", "Chi phí chuyển ngữ cảnh (Context Switch Overhead) chiếm tỷ trọng quá lớn làm giảm thông lượng hệ thống", "Biến thành giải thuật FCFS", "Gây Deadlock ổ cứng", "B", "Mỗi lần hết quantum hệ điều hành phải lưu/nạp thanh ghi và làm mới TLB cache; nếu q quá nhỏ CPU tốn phần lớn thời gian chỉ để chuyển ngữ cảnh."),
                ("Hai thao tác nguyên tử (Atomic Operations) kinh điển của Dijkstra trên biến đèn hiệu Semaphore S là gì?", "lock() và unlock()", "wait(S) [P] và signal(S) [V]", "fork() và join()", "malloc() và free()", "B", "Semaphore sử dụng hai thao tác không thể bị ngắt giữa chừng là P/wait (giảm S hoặc ngủ chờ) và V/signal (tăng S và đánh thức tiến trình)."),
                ("Hiện tượng hệ thống dành hầu hết thời gian để hoán đổi trang bộ nhớ (Page Fault liên tục) giữa RAM và ổ đĩa thay vì thực thi lệnh hữu ích gọi là gì?", "Fragmentation (Phân mảnh)", "Thrashing (Trì trệ trang bộ nhớ)", "Starvation (Đói tài nguyên)", "Spooling", "B", "Thrashing xảy ra khi tổng Working Set của các tiến trình vượt quá số khung trang vật lý khả dụng trong RAM."),
                ("Thuật toán thay thế trang (Page Replacement) nào loại bỏ trang đã lâu nhất không được truy cập trong quá khứ?", "FIFO", "LRU (Least Recently Used)", "MRU", "First-Fit", "B", "LRU dựa trên nguyên lý cục bộ thời gian, thay thế trang có thời điểm sử dụng gần nhất xa hiện tại nhất."),
                ("Giải thuật nào được sử dụng để Tránh Deadlock (Deadlock Avoidance) bằng cách kiểm tra Trạng thái An toàn (Safe State) trước khi cấp phát?", "Thuật toán Kruskal", "Thuật toán Nhà băng (Banker's Algorithm)", "Thuật toán Peterson", "Thuật toán Clock", "B", "Banker's Algorithm mô phỏng cấp phát thử và kiểm tra xem có tồn tại chuỗi hoàn tất an toàn cho mọi tiến trình hay không."),
                ("Bộ đệm phần cứng tốc độ cao nằm trong MMU giúp tăng tốc dịch địa chỉ trang ảo sang khung trang vật lý gọi là gì?", "L3 Data Cache", "TLB (Translation Lookaside Buffer)", "DMA Controller", "Interrupt Vector Table", "B", "Khi xảy ra TLB Hit, địa chỉ vật lý được tra cứu ngay trong phần cứng mà không cần đọc bảng trang nhiều cấp trong RAM.")
            ]),

            // Quiz 7: WEB301 (6 câu)
            CreateQuiz(6, 8, 2, "Kiểm tra Kiến trúc RESTful API, Xác thực JWT & Cơ chế CORS", QuizDifficulty.Medium, 10,
            [
                ("Phương thức HTTP nào dưới đây KHÔNG có tính chất Lũy đẳng (Idempotent) theo chuẩn kiến trúc REST?", "GET", "PUT", "POST", "DELETE", "C", "Gọi POST lặp lại nhiều lần sẽ tạo ra nhiều bản ghi tài nguyên mới độc lập, trong khi GET, PUT, DELETE giữ nguyên trạng thái đích."),
                ("Ba thành phần cấu thành một chuỗi JSON Web Token (JWT) chuẩn RFC 7519 cách nhau bởi dấu chấm gồm?", "Scheme.Host.Path", "Header.Payload.Signature", "Public.Private.Salt", "Token.Refresh.Session", "B", "JWT gồm Header (thuật toán ký), Payload (các Claims định danh) và Signature (chữ ký chống giả mạo)."),
                ("Trình duyệt gửi gói tin OPTIONS (CORS Preflight) trước lệnh gọi API chính khi nào?", "Khi tải ảnh PNG cùng tên miền", "Khi gọi Cross-Origin kèm Header tùy chỉnh như Authorization Bearer hoặc Content-Type application/json", "Khi người dùng tắt JavaScript", "Chỉ khi dùng giao thức FTP", "B", "Trình duyệt tự động hỏi ý kiến máy chủ bằng OPTIONS Preflight để kiểm tra Access-Control-Allow-Origin và Allow-Headers."),
                ("Mã trạng thái HTTP chuẩn nhất trả về khi khởi tạo thành công một phòng học hoặc đề thi mới qua POST là?", "HTTP 200 OK", "HTTP 201 Created", "HTTP 204 No Content", "HTTP 304 Not Modified", "B", "HTTP 201 Created biểu thị máy chủ đã tạo thành công một tài nguyên mới từ yêu cầu POST."),
                ("Ưu điểm lớn nhất của xác thực JWT Stateless so với Session lưu trong bộ nhớ RAM máy chủ là gì?", "Không cần HTTPS", "Dễ dàng mở rộng ngang (Horizontal Scaling) trên nhiều máy chủ sau Load Balancer mà không cần Sticky Session", "Tự động nén cơ sở dữ liệu", "Token không bao giờ hết hạn", "B", "Mọi nút Backend chia sẻ khóa xác minh đều đọc được danh tính người dùng ngay từ chữ ký JWT mà không phụ thuộc RAM cục bộ."),
                ("Mã phản hồi HTTP 304 Not Modified kết hợp với Header ETag mang lại lợi ích gì?", "Báo lỗi xác thực", "Cho phép trình duyệt tái sử dụng tài nguyên đã lưu trong bộ nhớ đệm mà không phải tải lại toàn bộ nội dung", "Chuyển hướng vĩnh viễn URL", "Giới hạn tốc độ truy cập", "B", "Nếu ETag không đổi, máy chủ chỉ trả Header 304 rỗng giúp tiết kiệm đáng kể băng thông mạng.")
            ]),

            // Quiz 8: SEC308 (6 câu)
            CreateQuiz(7, 9, 3, "Đánh giá An toàn Thông tin: Mật mã RSA, BCrypt & OWASP Top 10", QuizDifficulty.Hard, 12,
            [
                ("Độ an toàn tính toán của hệ mật mã khóa bất đối xứng RSA dựa trên tính khó của bài toán nào?", "Sắp xếp mảng số thực lớn", "Phân tích tích hai số nguyên tố cực lớn n = p * q ra thừa số nguyên tố", "Tìm đường đi ngắn nhất trên đồ thị", "Giải phương trình bậc hai", "B", "Khi p và q đủ lớn (2048-bit trở lên), việc phân tích n = p*q để suy ra hàm Euler phi(n) là bất khả thi với máy tính cổ điển."),
                ("Vì sao lưu mật khẩu bằng BCrypt an toàn vượt trội so với băm nhanh SHA-256 không Salt?", "BCrypt ngắn hơn", "BCrypt tích hợp Salt ngẫu nhiên riêng cho từng tài khoản (chống Rainbow Table) và Work Factor làm chậm vét cạn GPU", "BCrypt giải mã ngược được mật khẩu gốc", "BCrypt không tốn CPU", "B", "Work Factor (2^cost) cho phép điều chỉnh độ trễ băm ~100-250ms khiến kẻ tấn công không thể thử hàng tỷ mật khẩu mỗi giây."),
                ("Giải pháp phòng chống triệt để nhất đối với lỗ hổng SQL Injection là gì?", "Ẩn thanh địa chỉ trình duyệt", "Sử dụng Parameterized Queries (Truy vấn tham số hóa) hoặc biểu thức LINQ trong ORM như EF Core", "Chỉ cho phép mật khẩu chữ hoa", "Đổi cổng cơ sở dữ liệu", "B", "Tham số hóa tách biệt hoàn toàn cấu trúc câu lệnh SQL và dữ liệu đầu vào nên chuỗi độc hại không bao giờ thực thi như lệnh."),
                ("Kỹ thuật bắt buộc ở tầng giao diện để ngăn chặn tấn công Stored XSS khi hiển thị nội dung chat của người dùng là?", "Mã hóa các ký tự HTML đặc biệt (&, <, >, \", ') bằng hàm escapeHtml trước khi đưa vào DOM", "Dùng phông chữ Monospace", "Lưu tin nhắn vào LocalStorage", "Tắt CSS trên trang", "A", "Chuyển <script> thành &lt;script&gt; khiến trình duyệt chỉ hiển thị văn bản thuần thay vì thực thi mã độc JavaScript."),
                ("Với p = 3, q = 11 trong hệ mã RSA, giá trị hàm phi Euler phi(n) = (p - 1)(q - 1) bằng bao nhiêu?", "33", "20", "14", "30", "B", "Ta có n = 3 * 11 = 33 và phi(33) = (3 - 1) * (11 - 1) = 2 * 10 = 20."),
                ("Cơ chế giới hạn số lần nhập sai mã OTP (ví dụ tối đa 5 lần thử) nhằm chống lại hình thức tấn công nào?", "SQL Injection", "Brute-Force Attack (Tấn công vét cạn mã xác thực)", "ARP Spoofing", "DNS Cache Poisoning", "B", "Khóa hiệu lực OTP sau 5 lần nhập sai ngăn kịch bản tự động dò toàn bộ không gian 10^6 mã 6 chữ số.")
            ]),

            // Quiz 9: CLD405 (6 câu)
            CreateQuiz(8, 10, 8, "Trắc nghiệm Điện toán Đám mây, Docker Multi-Stage & CI/CD", QuizDifficulty.Medium, 10,
            [
                ("Hai tính năng cốt lõi của nhân Linux giúp Docker Container cô lập tài nguyên và giới hạn định mức CPU/RAM là?", "BIOS và UEFI", "Linux Namespaces và Control Groups (cgroups)", "FAT32 và NTFS", "Cron và Systemd", "B", "Namespaces cô lập không gian tiến trình/mạng/tệp, còn cgroups kiểm soát hạn ngạch phần cứng tiêu thụ."),
                ("Lợi ích thiết thực nhất của kỹ thuật Multi-Stage Build trong Dockerfile là gì?", "Tăng dung lượng Image lên gấp 4 lần", "Loại bỏ bộ công cụ biên dịch SDK khỏi Image chạy thực tế, giúp Image siêu nhẹ và giảm bề mặt tấn công bảo mật", "Không cần viết Dockerfile", "Tự động sinh cơ sở dữ liệu", "B", "Stage cuối chỉ chứa Runtime và tệp thực thi đã biên dịch, giảm kích thước Image từ gần 1 GB xuống vài chục MB."),
                ("Chiến lược triển khai nào duy trì song song hai môi trường giống hệt nhau để chuyển lưu lượng tức thì không gây gián đoạn (Zero-Downtime)?", "Big Bang Deployment", "Blue-Green Deployment", "Manual FTP Copy", "Cold Reboot", "B", "Blue-Green cho phép kiểm tra kỹ bản mới trên môi trường Green rồi chuyển Router từ Blue sang Green ngay lập tức."),
                ("Tệp cấu hình nào dùng để định nghĩa và khởi chạy đồng thời nhiều dịch vụ (Backend, AI Service, Database) trên cùng mạng nội bộ ảo?", "package.json", "docker-compose.yml", "robots.txt", "tsconfig.json", "B", "Docker Compose quản lý vòng đời đa container, biến môi trường, Volume bền vững và DNS nội bộ theo tên dịch vụ."),
                ("Để dữ liệu của SQL Server chạy trong Container không bị mất khi xóa hoặc tạo lại Container, cần cấu hình thành phần nào?", "Docker Volume ánh xạ ra vùng lưu trữ bền vững bên ngoài vòng đời Container", "Biến tạm trong RAM", "Port Forwarding", "Container Hostname", "A", "Container filesystem mặc định là tạm thời (ephemeral); Docker Volume bảo toàn dữ liệu lâu dài trên đĩa vật lý."),
                ("Endpoint /health trong kiến trúc Cloud Microservices phục vụ mục đích gì?", "Hiển thị giao diện trang chủ", "Cho phép bộ điều phối kiểm tra dịch vụ còn phản hồi khỏe mạnh hay cần khởi động lại / ngắt chia tải", "Lưu trữ tệp PDF", "Thay thế tường lửa", "B", "Liveness và Readiness Probes gọi /health định kỳ để chỉ điều hướng người dùng tới các bản sao đang sẵn sàng.")
            ]),

            // Quiz 10: SWE206 (6 câu)
            CreateQuiz(9, 11, 9, "Kiểm tra Công nghệ Phần mềm: Clean Architecture 4 Tầng & SOLID", QuizDifficulty.Medium, 10,
            [
                ("Theo Quy tắc Phụ thuộc (Dependency Rule) trong Clean Architecture, tầng Domain trong cùng được phép phụ thuộc vào tầng nào?", "Tầng Infrastructure", "Tầng API Web", "Không phụ thuộc vào bất kỳ tầng ngoài hay thư viện hạ tầng nào", "Thư viện giao diện người dùng", "C", "Domain chứa thực thể và quy tắc nghiệp vụ cốt lõi bền vững nhất nên hoàn toàn độc lập với CSDL, Web hay Framework."),
                ("Chữ 'D' trong bộ 5 nguyên lý SOLID là viết tắt của nguyên lý nào?", "Dynamic Inheritance", "Dependency Inversion Principle (Nguyên lý Đảo ngược Phụ thuộc)", "Database Isolation Principle", "Data Encapsulation", "B", "Module cấp cao không phụ thuộc trực tiếp vào module cấp thấp mà cả hai cùng phụ thuộc vào giao diện trừu tượng (Interfaces)."),
                ("Vì sao Thin Controller không nên tiêm trực tiếp AppDbContext hay viết câu lệnh truy vấn dữ liệu bên trong?", "Vì cú pháp C# không biên dịch được", "Vì vi phạm phân tách trách nhiệm (SRP), làm rò rỉ hạ tầng lên tầng giao vận HTTP và rất khó viết Unit Test độc lập", "Vì làm giảm dung lượng ổ cứng", "Vì JSON không hỗ trợ", "B", "Controller chỉ làm nhiệm vụ điều phối HTTP Request/Response; toàn bộ nghiệp vụ phải nằm trong Application Services."),
                ("Trong mô hình Kim tự tháp Kiểm thử (Testing Pyramid), loại kiểm thử nào nên chiếm tỷ trọng lớn nhất và chạy nhanh nhất?", "Kiểm thử thủ công trên trình duyệt", "Unit Tests (Kiểm thử đơn vị tầng nghiệp vụ)", "End-to-End Browser Tests", "Stress Load Tests", "B", "Unit Tests kiểm tra cô lập từng hàm/dịch vụ trong vài mili-giây, phát hiện lỗi logic sớm với chi phí bảo trì thấp nhất."),
                ("Nguyên lý Single Responsibility Principle (SRP) phát biểu rằng?", "Mỗi dự án chỉ có duy nhất 1 file mã nguồn", "Mỗi lớp hoặc mô-đun chỉ nên đảm nhận một trách nhiệm duy nhất (chỉ có một lý do để thay đổi)", "Mỗi hàm chỉ được nhận 1 tham số", "Chỉ dùng 1 bảng cơ sở dữ liệu", "B", "Tách riêng phần đọc API, phần xử lý nghiệp vụ và phần hiển thị giao diện giúp hệ thống dễ mở rộng và không gây lỗi dây chuyền."),
                ("Khi tầng Application định nghĩa giao diện IEmailSender còn tầng Infrastructure cài đặt GoogleSmtpEmailSender, đây là minh họa trực tiếp cho?", "Đảo ngược phụ thuộc (Dependency Inversion) kết hợp Dependency Injection", "Đệ quy quay lui", "Đa kế thừa lớp", "Biến toàn cục tĩnh", "A", "Application gọi IEmailSender mà không hề biết bên dưới gửi qua Google SMTP hay dịch vụ đám mây nào khác.")
            ])
        ];
    }

    public static List<FlashcardDeck> BuildSixFlashcardDecks(IReadOnlyList<StudyRoom> rooms, IReadOnlyList<CourseMaterial> materials, IReadOnlyList<User> students, DateTime now)
    {
        FlashcardDeck CreateDeck(int roomIdx, int matIdx, int creatorIdx, string title, (string Front, string Back, int Mastery)[] cards)
        {
            var deck = new FlashcardDeck
            {
                RoomId = rooms[roomIdx].Id,
                MaterialId = materials[matIdx].Id,
                CreatedById = students[creatorIdx].Id,
                Title = title,
                CardCount = cards.Length,
                CreatedAt = now.AddDays(-5 + roomIdx * 0.4)
            };
            foreach (var c in cards)
            {
                deck.Cards.Add(new Flashcard
                {
                    FrontContent = c.Front,
                    BackContent = c.Back,
                    MasteryLevel = c.Mastery
                });
            }
            return deck;
        }

        return
        [
            CreateDeck(0, 0, 0, "Bộ thẻ Trọng tâm: SignalR, WebSockets & TCP/UDP",
            [
                ("Cơ chế bắt tay nâng cấp lên WebSocket (RFC 6455)?", "Client gửi HTTP GET kèm Header 'Upgrade: websocket' & 'Connection: Upgrade'; Server trả về HTTP 101 Switching Protocols.", 2),
                ("3 cơ chế truyền tải theo thứ tự ưu tiên của ASP.NET Core SignalR?", "1. WebSockets (Full-duplex) -> 2. Server-Sent Events (SSE) -> 3. HTTP Long Polling.", 2),
                ("Cách xác thực JWT cho kết nối SignalR từ trình duyệt?", "Gửi qua query string ?access_token=... và đọc tại sự kiện JwtBearerEvents.OnMessageReceived.", 2),
                ("Vì sao phải dùng IServiceScopeFactory khi gọi AI bất đồng bộ từ Hub?", "Do Hub bị hủy ngay sau khi method hoàn tất; tạo Scope mới giúp lấy DbContext độc lập, chống lỗi ObjectDisposedException.", 1),
                ("So sánh kích thước Header giữa UDP và TCP?", "UDP cố định 8 bytes (nhẹ, không kết nối); TCP từ 20 đến 60 bytes (tin cậy, có kiểm soát luồng & tắc nghẽn).", 2),
                ("Công thức cửa sổ gửi hiệu dụng trong TCP?", "W_effective = min(rwnd, cwnd), kết hợp cả kiểm soát luồng phía nhận và kiểm soát tắc nghẽn mạng.", 1),
                ("Khi nào TCP kích hoạt Fast Retransmit?", "Khi bên gửi nhận được 3 gói tin Duplicate ACK liên tiếp cho cùng một Sequence Number mà chưa cần đợi hết hạn RTO.", 1),
                ("Ý nghĩa của trạng thái TIME_WAIT (2*MSL) trong TCP?", "Đảm bảo gói ACK cuối tới được bên nhận và để mọi gói tin đi lạc của kết nối cũ tự hết hạn trên mạng.", 0),
                ("Sự khác biệt giữa Clients.Group(g) và Clients.OthersInGroup(g)?", "Group(g) gửi cho toàn bộ kết nối trong phòng; OthersInGroup(g) loại trừ chính người gửi (dùng cho Typing Indicator).", 2),
                ("Công thức làm mịn ước lượng độ trễ vòng (EstimatedRTT)?", "EstimatedRTT = (1 - alpha) * EstimatedRTT + alpha * SampleRTT, với hệ số chuẩn alpha = 0.125.", 0)
            ]),

            CreateDeck(1, 2, 1, "Bộ thẻ Thuật toán Đồ thị & Quy hoạch động",
            [
                ("Độ phức tạp thời gian & bộ nhớ của BFS và DFS trên Danh sách kề?", "Thời gian: O(|V| + |E|) — Bộ nhớ phụ: O(|V|) cho mảng đánh dấu và Queue/Stack.", 2),
                ("Độ phức tạp thời gian của Dijkstra với Binary Min-Heap?", "O((|V| + |E|) log |V|), yêu cầu bắt buộc mọi cạnh có trọng số không âm w(u, v) >= 0.", 2),
                ("Cách phát hiện chu trình âm bằng thuật toán Bellman-Ford?", "Sau |V| - 1 vòng thư giãn toàn bộ cạnh, nếu ở lần kiểm tra thứ |V| vẫn còn cạnh giảm được khoảng cách thì có chu trình âm.", 2),
                ("Tối ưu cấu trúc Union-Find (DSU) trong thuật toán Kruskal?", "Kết hợp Nén đường đi (Path Compression) và Gộp theo hạng/kích thước (Union by Rank) đạt độ phức tạp gần hằng số O(alpha(V)).", 1),
                ("Điều kiện tồn tại thứ tự Sắp xếp Tô-pô (Topological Sort)?", "Đồ thị có hướng bắt buộc không chứa chu trình (Directed Acyclic Graph - DAG).", 2),
                ("Công thức truy hồi bài toán Balo 0/1 (0/1 Knapsack)?", "dp[i][w] = max(dp[i-1][w], dp[i-1][w - w_i] + v_i); duyệt ngược w từ W về w_i khi nén xuống mảng 1 chiều.", 1),
                ("Công thức truy hồi Dãy con chung dài nhất (LCS)?", "Nếu X[i] == Y[j] thì dp[i][j] = dp[i-1][j-1] + 1; ngược lại dp[i][j] = max(dp[i-1][j], dp[i-1][j-1]).", 1),
                ("Khi nào nên chọn Danh sách kề thay cho Ma trận kề?", "Khi đồ thị thưa (|E| << |V|^2), tiết kiệm không gian từ O(|V|^2) xuống O(|V| + |E|).", 2),
                ("Độ phức tạp thuật toán đường đi ngắn nhất mọi cặp đỉnh Floyd-Warshall?", "Thời gian O(|V|^3) và không gian O(|V|^2) với 3 vòng lặp lồng nhau k, i, j.", 0),
                ("Hai điều kiện bản chất để áp dụng Quy hoạch động?", "Bài toán con giao nhau (Overlapping Subproblems) và Cấu trúc con tối ưu (Optimal Substructure).", 1)
            ]),

            CreateDeck(2, 4, 4, "Bộ thẻ Nhập môn Trí tuệ Nhân tạo & Học máy",
            [
                ("Công thức hàm đánh giá f(n) trong tìm kiếm A*?", "f(n) = g(n) + h(n), với g(n) là chi phí thực từ gốc đến n và h(n) là ước lượng heuristic từ n tới đích.", 2),
                ("Tính chất Chấp nhận được (Admissible) của hàm Heuristic?", "0 <= h(n) <= h*(n) với mọi n (không bao giờ ước lượng vượt quá chi phí thực tế nhỏ nhất tới đích).", 2),
                ("Công thức Định lý Bayes chuẩn?", "P(A|B) = [P(B|A) * P(A)] / P(B), dùng tính xác suất hậu nghiệm từ xác suất tiền nghiệm và độ hợp lý.", 2),
                ("Vai trò của Làm mịn Laplace (Laplace Smoothing) trong Naive Bayes?", "Cộng thêm hằng số nhỏ (thường = 1) vào tử số tần suất để tránh tích xác suất bị triệt tiêu về 0 khi gặp từ mới.", 1),
                ("Quy tắc cập nhật tham số trong Gradient Descent?", "theta_j := theta_j - alpha * (dJ / d_theta_j), dịch chuyển ngược hướng vector đạo hàm riêng.", 1),
                ("Sự khác nhau giữa Overfitting và Underfitting?", "Overfitting: lỗi Train rất thấp nhưng lỗi Test cao (quá khớp nhiễu); Underfitting: lỗi cao trên cả Train và Test (mô hình quá đơn giản).", 2),
                ("Tác dụng của L1 (Lasso) so với L2 (Ridge) Regularization?", "L1 có khả năng ép một số trọng số không quan trọng về đúng 0 (chọn lọc đặc trưng); L2 thu nhỏ đều bình phương các trọng số.", 0),
                ("Công thức hàm kích hoạt Sigmoid và miền giá trị?", "sigma(z) = 1 / (1 + e^(-z)), ánh xạ miền thực (-inf, +inf) vào khoảng xác suất (0, 1).", 1),
                ("F1-Score được tính từ Precision và Recall như thế nào?", "Trung bình điều hòa: F1 = 2 * (Precision * Recall) / (Precision + Recall), đánh giá tốt dữ liệu mất cân bằng lớp.", 0),
                ("Khoảng cách Euclid khác khoảng cách Manhattan ở điểm nào?", "Euclid đo đường chim bay sqrt(dx^2 + dy^2); Manhattan đo tổng độ lệch trục |dx| + |dy| trên lưới vuông.", 2)
            ]),

            CreateDeck(3, 5, 5, "Bộ thẻ Tối ưu Truy vấn SQL Server & Giao dịch ACID",
            [
                ("Sự khác biệt cốt lõi giữa Clustered Index và Non-Clustered Index?", "Clustered Index sắp xếp trực tiếp trang dữ liệu vật lý của bảng (tối đa 1/bảng); Non-Clustered Index lưu khóa kèm con trỏ tham chiếu.", 2),
                ("Khái niệm Covering Index với mệnh đề INCLUDE?", "Chỉ mục chứa đủ tất cả các cột trong SELECT/WHERE giúp truy vấn trả kết quả ngay tại nút lá mà không cần Key Lookup.", 2),
                ("Thế nào là điều kiện lọc SARGable trong SQL?", "Search ARGument ABLE: Không bọc hàm tính toán quanh cột đánh chỉ mục ở WHERE để bộ tối ưu thực hiện được Index Seek.", 1),
                ("4 tính chất ACID của giao dịch CSDL gồm những gì?", "Atomicity (Nguyên tử), Consistency (Nhất quán), Isolation (Cô lập), Durability (Bền vững nhờ Write-Ahead Logging).", 2),
                ("Cách phòng chống lỗi Multiple Cascade Paths trên SQL Server?", "Thiết lập ON DELETE NO ACTION (DeleteBehavior.Restrict) trên các nhánh khóa ngoại phụ hội tụ về cùng bảng con.", 2),
                ("Quy tắc Tiền tố Trái (Leftmost Prefix) của Composite Index (A, B, C)?", "Index dùng hiệu quả cho truy vấn lọc theo (A), (A, B) hoặc (A, B, C), nhưng không thể Index Seek nếu chỉ lọc riêng cột (B) hoặc (C).", 1),
                ("Điều kiện đạt dạng chuẩn 3 (3NF)?", "Bảng đạt chuẩn 2NF và không tồn tại phụ thuộc hàm bắc cầu từ thuộc tính không khóa vào khóa chính.", 1),
                ("Snapshot Isolation trên SQL Server hoạt động như thế nào?", "Sử dụng cơ chế đa phiên bản dòng (Row Versioning trong tempdb) giúp lệnh Đọc không bị chặn bởi lệnh Ghi.", 0),
                ("Kích thước Data Page và Extent trong SQL Server?", "1 Data Page = 8 KB; 1 Extent = 8 Data Pages liên tiếp = 64 KB.", 1),
                ("Khi nào Filtered Unique Index phát huy tác dụng?", "Khi muốn ép duy nhất cho các giá trị khác NULL (ví dụ: WHERE [StudentCode] IS NOT NULL trên bảng Users).", 2)
            ]),

            CreateDeck(4, 6, 6, "Bộ thẻ Toán Rời rạc, Tổ hợp & Đồ thị Euler",
            [
                ("Phát biểu tổng quát Nguyên lý Dirichlet (Chuồng bồ câu)?", "Nếu phân phối N vật vào k hộp thì tồn tại ít nhất 1 hộp chứa từ ceil(N / k) vật trở lên.", 2),
                ("Công thức Bao hàm - Loại trừ cho 3 tập hợp A, B, C?", "|A U B U C| = |A| + |B| + |C| - |A n B| - |B n C| - |C n A| + |A n B n C|.", 2),
                ("Điều kiện cần và đủ của Chu trình Euler và Đường đi Euler?", "Chu trình Euler: mọi đỉnh đều bậc chẵn; Đường đi Euler (không khép kín): có đúng 2 đỉnh bậc lẻ.", 2),
                ("Bổ đề Bắt tay (Handshaking Lemma) trong đồ thị vô hướng?", "Tổng bậc của tất cả các đỉnh bằng 2 lần số cạnh: sum(deg(v)) = 2|E|, suy ra số đỉnh bậc lẻ luôn là số chẵn.", 1),
                ("Ba trường hợp của Định lý Master cho T(n) = a*T(n/b) + Theta(n^d)?", "So sánh d với log_b(a): d > log_b(a) => Theta(n^d); d == log_b(a) => Theta(n^d log n); d < log_b(a) => Theta(n^(log_b a)).", 1),
                ("Số cạnh của cây n đỉnh và đồ thị đầy đủ K_n?", "Cây n đỉnh có n - 1 cạnh; Đồ thị đầy đủ K_n có n(n - 1) / 2 cạnh.", 2),
                ("Luật De Morgan trong Logic mệnh đề?", "!(P && Q) <=> (!P || !Q) và !(P || Q) <=> (!P && !Q).", 2),
                ("Định nghĩa Quan hệ Tương đương (Equivalence Relation)?", "Quan hệ hai ngôi đồng thời thỏa mãn 3 tính chất: Phản xạ (Reflexive), Đối xứng (Symmetric) và Bắc cầu (Transitive).", 1),
                ("Định nghĩa Quan hệ Thứ tự Bộ phận (Partial Order - Poset)?", "Quan hệ thỏa mãn 3 tính chất: Phản xạ, Phản đối xứng (Antisymmetric) và Bắc cầu.", 0),
                ("Công thức Tổ hợp lặp chọn k phần tử từ n loại?", "C(n + k - 1, k) = (n + k - 1)! / [k! * (n - 1)!].", 0)
            ]),

            CreateDeck(5, 7, 7, "Bộ thẻ Nguyên lý Hệ điều hành Linux & Đồng bộ hóa",
            [
                ("4 điều kiện Coffman bắt buộc để xảy ra Deadlock?", "1. Mutual Exclusion, 2. Hold and Wait, 3. No Preemption, 4. Circular Wait.", 2),
                ("Cơ chế Copy-on-Write (CoW) trong fork() của Linux?", "Cha và con chia sẻ chung khung trang vật lý chế độ Read-Only; chỉ khi một bên ghi dữ liệu thì nhân mới cấp trang riêng.", 2),
                ("Cấu trúc dữ liệu cốt lõi của bộ lập lịch Linux CFS?", "Cây Đỏ-Đen (Red-Black Tree) sắp xếp theo vruntime; tiến trình ở nút trái nhất luôn được chọn chạy tiếp theo.", 1),
                ("Sự khác nhau giữa Tiến trình (Process) và Luồng (Thread)?", "Process có không gian địa chỉ ảo độc lập; các Thread trong cùng Process dùng chung Code/Data/Heap nhưng có Stack và thanh ghi riêng.", 2),
                ("Ý nghĩa của TLB (Translation Lookaside Buffer)?", "Bộ nhớ đệm phần cứng lưu ánh xạ Page Number -> Frame Number giúp giảm số lần truy cập bảng trang trong RAM.", 1),
                ("Nguyên nhân gây ra hiện tượng Thrashing?", "Tổng số trang cần thiết của các tiến trình đang chạy lớn hơn RAM vật lý khiến CPU chỉ bận xử lý Page Fault.", 1),
                ("Bất thường Belady (Belady's Anomaly) xảy ra ở thuật toán nào?", "Xảy ra ở thay thế trang FIFO: cấp thêm khung trang RAM nhưng số lỗi trang Page Fault lại tăng lên (LRU không bao giờ bị).", 0),
                ("Thuật toán Nhà băng (Banker's Algorithm) dùng để làm gì?", "Kiểm tra xem việc cấp phát tài nguyên có giữ hệ thống ở Safe State hay không nhằm tránh Deadlock.", 1),
                ("Ba yêu cầu của bài toán Vùng găng (Critical Section)?", "1. Mutual Exclusion (Loại trừ tương hỗ), 2. Progress (Tiến triển), 3. Bounded Waiting (Chờ đợi hữu hạn).", 1),
                ("Phân mảnh nội (Internal) khác Phân mảnh ngoại (External) thế nào?", "Phân mảnh nội là phần dư thừa bên trong một khối kích thước cố định đã cấp; Phân mảnh ngoại là các khoảng trống rời rạc ngoài khối.", 0)
            ]),

            CreateDeck(6, 8, 2, "Bộ thẻ RESTful API, JWT Stateless & HTTP Caching",
            [
                ("Ba phần của JSON Web Token (JWT)?", "Header.Payload.Signature mã hóa Base64Url; chữ ký HMAC-SHA256 đảm bảo Payload không thể bị chỉnh sửa trái phép.", 2),
                ("Tính Lũy đẳng (Idempotent) của HTTP Methods?", "GET, PUT, DELETE là Idempotent; POST không Idempotent vì gọi nhiều lần sẽ sinh ra nhiều tài nguyên trùng lặp.", 2),
                ("Khi nào trình duyệt gửi gói OPTIONS Preflight?", "Khi gửi yêu cầu Cross-Origin có Header tùy chỉnh (như Authorization: Bearer) hoặc phương thức PUT/DELETE/PATCH.", 1),
                ("Ý nghĩa của cặp Header ETag và If-None-Match?", "Kiểm tra dấu vân tay phiên bản tài nguyên; nếu không đổi Server trả 304 Not Modified giúp tiết kiệm băng thông.", 1),
                ("Khác biệt giữa HTTP 401 Unauthorized và 403 Forbidden?", "401: Chưa đăng nhập hoặc Token hết hạn/không hợp lệ; 403: Đã đăng nhập nhưng không đủ quyền hạn truy cập tài nguyên đó.", 2),
                ("Ưu điểm nổi bật của HTTP/2 so với HTTP/1.1?", "Ghép kênh đa luồng (Multiplexing) trên 1 kết nối TCP duy nhất, nén Header HPACK và triệt tiêu Head-of-Line Blocking ở tầng ứng dụng.", 0)
            ]),

            CreateDeck(7, 9, 3, "Bộ thẻ Mật mã RSA, BCrypt & Phòng vệ OWASP Top 10",
            [
                ("Công thức mã hóa và giải mã trong hệ mật RSA?", "Mã hóa: C = M^e mod n; Giải mã: M = C^d mod n, với n = p*q và e*d đồng dư 1 theo modulo (p-1)(q-1).", 2),
                ("Vai trò của Random Salt trong thuật toán băm mật khẩu BCrypt?", "Đảm bảo hai người dùng đặt cùng mật khẩu vẫn sinh ra hai chuỗi Hash hoàn toàn khác nhau, vô hiệu hóa bảng cầu vồng Rainbow Table.", 2),
                ("Cách phòng chống triệt để SQL Injection?", "Luôn sử dụng Parameterized Queries / Prepared Statements hoặc biểu thức LINQ trong ORM thay vì nối chuỗi SQL thủ công.", 2),
                ("Cách phòng chống Stored XSS trên ứng dụng chat thời gian thực?", "Bọc mọi dữ liệu văn bản từ người dùng qua hàm mã hóa ký tự HTML (escapeHtml) trước khi hiển thị lên DOM.", 1),
                ("Nguyên tắc Đặc quyền Tối thiểu (Principle of Least Privilege)?", "Mỗi tài khoản hoặc tiến trình chỉ được cấp đúng tập quyền tối thiểu cần thiết để hoàn thành tác vụ của mình.", 1),
                ("Tác dụng của cơ chế giới hạn 5 lần thử sai OTP?", "Ngăn chặn tấn công vét cạn tự động (Brute-force) vào mã khôi phục mật khẩu 6 chữ số.", 2)
            ]),

            CreateDeck(8, 10, 8, "Bộ thẻ Docker Container, Multi-Stage Build & CI/CD",
            [
                ("Sự khác nhau giữa Docker Image và Docker Container?", "Image là khuôn mẫu bất biến nhiều lớp chỉ đọc (Read-only Layers); Container là một phiên bản thực thi sống có thêm lớp ghi ở trên cùng.", 2),
                ("Lợi ích của Multi-Stage Dockerfile?", "Dùng Stage đầu có SDK để biên dịch và chỉ chép tệp thực thi sang Stage Runtime tối giản, giảm 80% dung lượng và tăng bảo mật.", 2),
                ("Linux Namespaces khác Control Groups (cgroups) thế nào?", "Namespaces cô lập tầm nhìn tài nguyên (PID, Network, Mount); cgroups giới hạn mức tiêu thụ phần cứng (CPU, RAM, I/O).", 1),
                ("Vai trò của Docker Volume đối với CSDL?", "Ánh xạ thư mục dữ liệu CSDL ra ngoài vòng đời Container để bảo toàn dữ liệu khi cập nhật hoặc khởi tạo lại Container.", 2),
                ("Chiến lược triển khai Blue-Green Deployment?", "Chạy song song môi trường cũ (Blue) và mới (Green); sau khi kiểm thử Green đạt chuẩn sẽ chuyển lưu lượng tức thì không downtime.", 1),
                ("Ý nghĩa của CI và CD trong DevOps?", "CI (Continuous Integration): tự động build & chạy Unit Test mỗi lần đẩy code; CD (Continuous Delivery/Deployment): tự động đóng gói & phát hành.", 2)
            ]),

            CreateDeck(9, 11, 9, "Bộ thẻ Clean Architecture 4 Tầng & Nguyên lý SOLID",
            [
                ("Chiều phụ thuộc bắt buộc trong Clean Architecture?", "Mọi mũi tên phụ thuộc luôn hướng vào tâm: API & Infrastructure -> Application -> Domain (Domain hoàn toàn độc lập).", 2),
                ("5 nguyên lý SOLID gồm những chữ cái nào?", "S: Single Responsibility, O: Open/Closed, L: Liskov Substitution, I: Interface Segregation, D: Dependency Inversion.", 2),
                ("Vai trò của Thin Controller?", "Chỉ tiếp nhận HTTP Request, gọi Application Service tương ứng và đóng gói kết quả chuẩn ApiResponse; không chứa truy vấn CSDL.", 2),
                ("Lợi ích của Dependency Inversion (DIP) khi viết Unit Test?", "Cho phép thay thế dễ dàng CSDL thật hoặc cổng gửi mail thật bằng InMemory / Test Doubles chạy kiểm thử độc lập trong vài mili-giây.", 1),
                ("Nguyên lý Open/Closed (OCP) nghĩa là gì?", "Thực thể phần mềm nên mở cho việc mở rộng hành vi mới (qua kế thừa/interface) nhưng đóng đối với việc sửa đổi mã nguồn cốt lõi đang ổn định.", 1),
                ("Nguyên lý Interface Segregation (ISP)?", "Chia các giao diện lớn cồng kềnh thành các giao diện nhỏ chuyên biệt để lớp thực thi không phải cài đặt phương thức dư thừa.", 0)
            ])
        ];
    }

    public static List<ChatMessage> BuildRichRoomConversations(IReadOnlyList<StudyRoom> rooms, IReadOnlyList<User> students, DateTime now)
    {
        var messages = new List<ChatMessage>();

        void AddStudentMsg(int roomIdx, int studentIdx, int minutesAgo, string content, bool pinned = false)
        {
            messages.Add(new ChatMessage
            {
                RoomId = rooms[roomIdx].Id,
                SenderId = students[studentIdx].Id,
                IsAiBot = false,
                IsPinned = pinned,
                Content = content,
                CreatedAt = now.AddMinutes(-minutesAgo)
            });
        }

        void AddBotMsg(int roomIdx, int minutesAgo, string markdownContent, bool pinned = false)
        {
            messages.Add(new ChatMessage
            {
                RoomId = rooms[roomIdx].Id,
                SenderId = null,
                IsAiBot = true,
                IsPinned = pinned,
                Content = markdownContent,
                CreatedAt = now.AddMinutes(-minutesAgo)
            });
        }

        // ==================== PHÒNG 1: NET101 (SignalR & TCP/UDP) ====================
        AddStudentMsg(0, 0, 240, "Chào cả nhóm VKU! Mình vừa upload đủ 2 chuyên đề PDF về ASP.NET Core SignalR và TCP/UDP lên cột bên phải rồi nhé.");
        AddStudentMsg(0, 1, 236, "Cảm ơn An nhiều! Tối nay 20h cả nhóm bật đồng hồ Pomodoro 25 phút trên thanh tiêu đề để cày xong phần Transport Fallback nha.");
        AddStudentMsg(0, 2, 232, "Mọi người cho mình hỏi khi đi bảo vệ đồ án, thầy cô hay hỏi sự khác nhau cốt lõi giữa TCP và UDP ở tầng Giao vận như thế nào nhỉ?");
        AddStudentMsg(0, 3, 230, "Để mình gọi trợ giảng @bot lập bảng đối chiếu chi tiết từ tài liệu của phòng luôn cho trực quan!");
        AddStudentMsg(0, 3, 229, "@bot Lập bảng so sánh chi tiết giao thức TCP và UDP từ học liệu phòng học, kèm công thức cửa sổ truyền hiệu dụng?");
        AddBotMsg(0, 228,
            "### Đối chiếu Chuyên sâu Giao thức Tầng Giao vận: TCP vs UDP\n\n" +
            "Dựa trên **Chuyên đề 2: Phân tích Giao thức TCP, UDP & Kiểm soát Tắc nghẽn**, bảng so sánh trọng tâm như sau:\n\n" +
            "| Tiêu chí Kỹ thuật | Giao thức TCP (RFC 793) | Giao thức UDP (RFC 768) |\n" +
            "| :--- | :--- | :--- |\n" +
            "| **Mô hình kết nối** | Hướng kết nối (*3-Way Handshake*) | Phi kết nối (*Connectionless*) |\n" +
            "| **Độ tin cậy & Thứ tự** | Đảm bảo 100% nhờ `SEQ`, `ACK` & Retransmit | Best-effort, không tự truyền lại |\n" +
            "| **Kích thước Header** | `20 - 60 bytes` | Cố định **`8 bytes`** siêu nhẹ |\n" +
            "| **Kiểm soát luồng/tắc nghẽn** | Có (`rwnd`, `cwnd`, Slow Start, AIMD) | Không có ở tầng giao vận |\n" +
            "| **Ứng dụng điển hình** | HTTP/1.1, HTTP/2, WebSockets, SQL, SSH | VoIP, WebRTC Video, DNS, QUIC (HTTP/3) |\n\n" +
            "#### Công thức Cửa sổ Gửi Hiệu dụng trong TCP\n" +
            "Lượng dữ liệu tối đa bên gửi được phép đẩy lên mạng khi chưa nhận `ACK` bị chặn bởi cả bộ đệm bên nhận và mức nghẽn mạng:\n\n" +
            "$$W_{\\text{effective}} = \\min(\\text{rwnd}, \\text{cwnd})$$\n\n" +
            "> **Mẹo bảo vệ đồ án:** SignalR mặc định chạy trên **WebSockets (nền TCP)** để đảm bảo không bao giờ mất tin nhắn thảo luận hay sự kiện đồng bộ phòng học.",
            pinned: true);

        AddStudentMsg(0, 4, 215, "Bảng so sánh rõ nét quá! Mình ghim câu trả lời này lên đầu phòng để cả nhóm ôn nhé.");
        AddStudentMsg(0, 5, 205, "Còn vụ lỗi `ObjectDisposedException` khi gọi AI chạy ngầm trong `StudyRoomHub` thì viết code C# chuẩn Clean Architecture thế nào nhỉ?");
        AddStudentMsg(0, 0, 204, "@bot Cho đoạn code mẫu C# trong SignalR Hub dùng IServiceScopeFactory để gọi AI bất đồng bộ an toàn không bị ObjectDisposedException?");
        AddBotMsg(0, 203,
            "### Mẫu Thiết kế Bất đồng bộ An toàn trong `StudyRoomHub` (.NET 8)\n\n" +
            "Vì `Hub` trong ASP.NET Core SignalR có vòng đời **Transient** (bị `Dispose` ngay khi phương thức `SendMessage` trả kết quả), bạn bắt buộc tạo Scope riêng bằng `IServiceScopeFactory` và luôn đặt `BotThinking(false)` trong khối `finally`:\n\n" +
            "```csharp\n" +
            "private async Task ProcessBotReplySafelyAsync(int roomId, int userId, string question)\n" +
            "{\n" +
            "    var groupName = $\"room-{roomId}\";\n" +
            "    try\n" +
            "    {\n" +
            "        using var scope = _scopeFactory.CreateScope();\n" +
            "        var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();\n\n" +
            "        var botMessage = await chatService.GenerateAndSaveBotReplyAsync(roomId, userId, question);\n" +
            "        await _hubContext.Clients.Group(groupName).SendAsync(\"ReceiveMessage\", botMessage);\n" +
            "    }\n" +
            "    finally\n" +
            "    {\n" +
            "        await _hubContext.Clients.Group(groupName).SendAsync(\"BotThinking\", new { roomId, isThinking = false });\n" +
            "    }\n" +
            "}\n" +
            "```\n\n" +
            "- Không bao giờ truy cập trực tiếp `this.Clients` hoặc `AppDbContext` của Hub từ bên trong `Task` chạy ngầm.",
            pinned: true);

        AddStudentMsg(0, 6, 180, "Quá đỉnh, có nút Copy code ngay trên góc phải khung C# luôn kìa!");
        AddStudentMsg(0, 7, 165, "Mình vừa làm thử đề Quiz 10 câu của phòng NET101 được 9.0/10, câu số 7 về 3 Duplicate ACKs hay cực.");
        AddStudentMsg(0, 8, 150, "Để mình vào đua Top bảng xếp hạng với Trúc liền đây!");
        AddStudentMsg(0, 1, 120, "Nhớ ôn kỹ cả bộ 10 thẻ Flashcards 3D phần `JwtBearerEvents.OnMessageReceived` nữa nhé mọi người.");
        AddStudentMsg(0, 2, 95, "Ok Bình ơi, mình đánh dấu thuộc 8/10 thẻ rồi!");

        // ==================== PHÒNG 2: DSA202 (Đồ thị & Quy hoạch động) ====================
        AddStudentMsg(1, 1, 300, "Chào team giải thuật! Hai file PDF về Đồ thị (Dijkstra, Kruskal) và Quy hoạch động (Knapsack, LCS) đã sẵn sàng rồi nha.");
        AddStudentMsg(1, 2, 290, "Phần cài đặt Dijkstra bằng `heapq` trong Python mình hay quên bước bỏ qua các cặp `(d, u)` lỗi thời trong Heap quá.");
        AddStudentMsg(1, 3, 288, "@bot Viết code Python chuẩn cho thuật toán Dijkstra dùng Min-Heap và phân tích độ phức tạp bằng công thức LaTeX?");
        AddBotMsg(1, 287,
            "### Cài đặt Chuẩn Thuật toán Dijkstra với Binary Min-Heap\n\n" +
            "Trên đồ thị có trọng số **không âm** ($w(u, v) \\ge 0$), ta dùng hàng đợi ưu tiên `heapq` kết hợp câu lệnh kiểm tra `if d > dist[u]: continue` để loại bỏ các bản ghi cũ (*lazy deletion*):\n\n" +
            "```python\n" +
            "import heapq\n\n" +
            "def dijkstra_shortest_path(n: int, adj: list[list[tuple[int, int]]], src: int) -> list[float]:\n" +
            "    dist = [float('inf')] * n\n" +
            "    dist[src] = 0.0\n" +
            "    min_heap: list[tuple[float, int]] = [(0.0, src)]\n\n" +
            "    while min_heap:\n" +
            "        d, u = heapq.heappop(min_heap)\n" +
            "        if d > dist[u]:\n" +
            "            continue  # Bỏ qua trạng thái lỗi thời trong Heap\n" +
            "        for v, weight in adj[u]:\n" +
            "            if dist[u] + weight < dist[v]:\n" +
            "                dist[v] = dist[u] + weight\n" +
            "                heapq.heappush(min_heap, (dist[v], v))\n" +
            "    return dist\n" +
            "```\n\n" +
            "#### Phân tích Độ phức tạp Tiệm cận\n" +
            "- Số thao tác đẩy vào / lấy ra khỏi Heap tối đa là $|E|$ lần, mỗi lần tốn $O(\\log |E|) = O(\\log |V|)$:\n\n" +
            "$$T(V, E) = O\\big((|V| + |E|) \\log |V|\\big), \\qquad S(V, E) = O(|V| + |E|)$$",
            pinned: true);

        AddStudentMsg(1, 0, 260, "Đoạn `if d > dist[u]: continue` cực kỳ quan trọng luôn, thiếu dòng đó là bị TLE trên đồ thị dày ngay!");
        AddStudentMsg(1, 5, 245, "Còn bài toán Cái túi 0/1 khi nén mảng 2 chiều xuống mảng 1 chiều tại sao bắt buộc duyệt ngược `w` từ `W` về `weight[i]` vậy mọi người?");
        AddStudentMsg(1, 5, 244, "@bot Giải thích toán học vì sao tối ưu bộ nhớ Balo 0/1 phải duyệt ngược trọng lượng w giảm dần, còn duyệt xuôi lại thành Balo không giới hạn?");
        AddBotMsg(1, 243,
            "### Bản chất Thứ tự Duyệt Trọng lượng trong Bài toán Balo (*Knapsack*)\n\n" +
            "Xét công thức quy hoạch động gốc 2 chiều khi xét đến vật thứ $i$ (trọng lượng $w_i$, giá trị $v_i$):\n\n" +
            "$$dp[i][w] = \\max\\big(dp[i - 1][w],\\; dp[i - 1][w - w_i] + v_i\\big)$$\n\n" +
            "1. **Khi duyệt ngược $w$ từ $W \\to w_i$ (*0/1 Knapsack*):**\n" +
            "   Tại thời điểm tính `dp[w]`, ô `dp[w - w_i]` ở bên trái **chưa bị ghi đè** ở vòng lặp $i$ hiện tại nên nó chính xác là $dp[i-1][w - w_i]$. Nhờ vậy mỗi đồ vật chỉ được chọn **tối đa 1 lần**.\n" +
            "2. **Khi duyệt xuôi $w$ từ $w_i \\to W$ (*Unbounded Knapsack*):**\n" +
            "   Ô `dp[w - w_i]` đã được cập nhật ở chính vòng lặp $i$ ($dp[i][w - w_i]$), đồng nghĩa với việc cho phép chọn lặp lại đồ vật $i$ vô hạn lần.",
            pinned: true);

        AddStudentMsg(1, 6, 210, "Giải thích tường minh thật sự! Đọc phát hiểu bản chất luôn không cần học vẹt.");
        AddStudentMsg(1, 7, 185, "Mọi người vào tab Trắc nghiệm AI thử sức đề Hard 10 câu đi, mình vừa đạt 10/10 bắn pháo giấy đẹp mê ly!");
        AddStudentMsg(1, 2, 160, "Ghê vậy Trúc! Để Nam vào gỡ điểm ngay.");
        AddStudentMsg(1, 3, 130, "Ai cần in tóm tắt ra giấy nhớ bấm nút Xuất / In Phiếu Ôn Tập ở cột phải nhé.");

        // ==================== PHÒNG 3: AI305 (AI & Machine Learning) ====================
        AddStudentMsg(2, 4, 320, "Chào mừng 10 thành viên lớp Trí tuệ Nhân tạo VKU! Tài liệu A*, Định lý Bayes và Gradient Descent đã có trên kệ.");
        AddStudentMsg(2, 8, 310, "Vy ơi, phần công thức Bayes với Laplace Smoothing trong phân loại email Spam hay ra thi tự luận lắm đúng không?");
        AddStudentMsg(2, 4, 308, "Đúng rồi Thịnh! Để mình nhờ @bot trình bày công thức Bayes kèm Laplace Smoothing chuẩn ký hiệu toán học cho cả phòng.");
        AddStudentMsg(2, 4, 307, "@bot Trình bày công thức Định lý Bayes, giả định Naive Bayes và công thức Làm mịn Laplace trong phân loại văn bản?");
        AddBotMsg(2, 306,
            "### Định lý Bayes & Bộ phân loại Văn bản Naive Bayes\n\n" +
            "#### 1. Định lý Bayes Tổng quát\n" +
            "Xác suất hậu nghiệm của lớp $A$ khi quan sát bằng chứng $B$ được tính bởi:\n\n" +
            "$$P(A|B) = \\frac{P(B|A)\\cdot P(A)}{P(B)}$$\n\n" +
            "#### 2. Quy tắc Quyết định MAP (*Maximum A Posteriori*) trong Naive Bayes\n" +
            "Với vector đặc trưng $\\mathbf{x} = (x_1, x_2, \\dots, x_d)$ độc lập có điều kiện theo lớp $c \\in \\mathcal{C}$:\n\n" +
            "$$\\hat{c} = \\arg\\max_{c \\in \\mathcal{C}} \\; P(c) \\prod_{i=1}^{d} P(x_i \\mid c)$$\n\n" +
            "#### 3. Làm mịn Cộng Laplace (*Additive Laplace Smoothing*)\n" +
            "Để ngăn một từ mới có `count(w, c) = 0` kéo toàn bộ tích xác suất về $0$, ta cộng hệ số $\\alpha = 1$ và kích thước từ vựng $|V|$:\n\n" +
            "$$P(w \\mid c) = \\frac{\\text{count}(w, c) + 1}{\\sum_{w' \\in V} \\text{count}(w', c) + |V|}$$",
            pinned: true);

        AddStudentMsg(2, 9, 280, "Công thức KaTeX hiển thị phân số và ký hiệu tích đẹp y như sách giáo khoa Latex luôn!");
        AddStudentMsg(2, 10, 255, "@bot Viết hàm Python minh họa thuật toán Gradient Descent cho Hồi quy tuyến tính 1 biến kèm công thức đạo hàm MSE?");
        AddBotMsg(2, 254,
            "### Thuật toán Hạ dốc (*Gradient Descent*) cho Hồi quy Tuyến tính\n\n" +
            "Hàm mất mát trung bình bình phương sai số (*Mean Squared Error*):\n\n" +
            "$$J(w, b) = \\frac{1}{2m} \\sum_{i=1}^{m} \\big((w x^{(i)} + b) - y^{(i)}\\big)^2$$\n\n" +
            "```python\n" +
            "import numpy as np\n\n" +
            "def linear_regression_gd(x: np.ndarray, y: np.ndarray, lr: float = 0.01, epochs: int = 500) -> tuple[float, float]:\n" +
            "    w, b = 0.0, 0.0\n" +
            "    m = float(len(x))\n" +
            "    for _ in range(epochs):\n" +
            "        y_pred = w * x + b\n" +
            "        error = y_pred - y\n" +
            "        dw = (1.0 / m) * np.dot(error, x)\n" +
            "        db = (1.0 / m) * np.sum(error)\n" +
            "        w -= lr * dw\n" +
            "        b -= lr * db\n" +
            "    return w, b\n" +
            "```",
            pinned: false);

        AddStudentMsg(2, 0, 220, "Nhìn vector hóa bằng `np.dot` gọn gàng hơn hẳn vòng for thuần!");
        AddStudentMsg(2, 1, 195, "Mình vừa dùng tính năng thu âm giọng nói tiếng Việt hỏi @bot cũng nhận cực nhạy luôn các bạn thử xem.");
        AddStudentMsg(2, 6, 170, "Xác nhận nhé, bấm Ctrl+K nhảy giữa phòng AI305 với DSA202 mất chưa tới nửa giây!");
        AddStudentMsg(2, 9, 140, "Tối nay làm thêm 1 vòng Pomodoro 25p ôn tìm kiếm A* nha cả nhà.");

        // ==================== PHÒNG 4: SQL204 (Tối ưu CSDL SQL Server) ====================
        AddStudentMsg(3, 5, 275, "Team Cơ sở dữ liệu chú ý: Mình đã tải giáo trình Tối ưu B+Tree Index & Giao dịch ACID lên phòng SQL204 rồi nhé!");
        AddStudentMsg(3, 2, 265, "Bảo ơi, khi nào truy vấn bị dính Key Lookup trong Execution Plan và cách xử lý dứt điểm là gì nhỉ?");
        AddStudentMsg(3, 5, 264, "@bot Giải thích hiện tượng Key Lookup trong SQL Server Execution Plan và viết câu lệnh T-SQL tạo Covering Index tối ưu?");
        AddBotMsg(3, 263,
            "### Phân tích `Key Lookup` & Kỹ thuật Tạo `Covering Index` trên SQL Server\n\n" +
            "Khi bạn lọc bằng một **Non-Clustered Index** nhưng mệnh đề `SELECT` lại lấy thêm các cột **không nằm trong chỉ mục đó**, SQL Server phải thực hiện thêm thao tác `Key Lookup` nhảy ngược về **Clustered Index** cho từng dòng thỏa mãn — gây tốn kém I/O ngẫu nhiên rất lớn khi số dòng trả về nhiều.\n\n" +
            "```sql\n" +
            "-- Truy vấn tải 30 tin nhắn mới nhất của phòng học:\n" +
            "SELECT TOP (30) [Id], [RoomId], [SenderId], [IsAiBot], [IsPinned], [CreatedAt]\n" +
            "FROM [dbo].[ChatMessages]\n" +
            "WHERE [RoomId] = @RoomId\n" +
            "ORDER BY [CreatedAt] DESC;\n\n" +
            "-- Giải pháp Covering Index bằng mệnh đề INCLUDE (100% Index Seek, 0 Key Lookup):\n" +
            "CREATE NONCLUSTERED INDEX [IX_ChatMessages_RoomId_CreatedAt]\n" +
            "ON [dbo].[ChatMessages] ([RoomId] ASC, [CreatedAt] DESC)\n" +
            "INCLUDE ([SenderId], [IsAiBot], [IsPinned]);\n" +
            "```\n\n" +
            "| Phương án | Thao tác Vật lý | Số lần Đọc Logical Reads (100k dòng) |\n" +
            "| :--- | :--- | :---: |\n" +
            "| Không có Index | `Clustered Index Scan` + `Sort` | $\\approx 1.450$ pages |\n" +
            "| Index thiếu `INCLUDE` | `Index Seek` + `Key Lookup` | $\\approx 98$ pages |\n" +
            "| **Covering Index chuẩn** | **Thuần `Index Seek` (Không Sort)** | **Chỉ $3 - 4$ pages** |",
            pinned: true);

        AddStudentMsg(3, 3, 240, "Ví dụ lấy ngay chính bảng `ChatMessages` của dự án mình luôn, đem đi bảo vệ đồ án thì hết nước chấm!");
        AddStudentMsg(3, 7, 215, "Đúng chuẩn SARGable luôn, nhớ đừng viết `WHERE YEAR(CreatedAt) = 2026` kẻo mất Index Seek nhé anh em.");
        AddStudentMsg(3, 8, 190, "Mình vừa ôn xong 10 thẻ Flashcards về ACID và Snapshot Isolation, dễ nhớ cực kỳ.");

        // ==================== PHÒNG 5: MAT108 (Toán rời rạc) ====================
        AddStudentMsg(4, 6, 260, "Chào hội ôn thi Toán Rời rạc! File tổng hợp Tổ hợp, Hệ thức truy hồi và Đồ thị Euler đã lên sóng.");
        AddStudentMsg(4, 9, 250, "Huy ơi phần giải phương trình đặc trưng bậc 2 với Định lý Master mình hay nhầm điều kiện 3 trường hợp quá.");
        AddStudentMsg(4, 6, 249, "@bot Tóm tắt 3 trường hợp của Định lý Master kèm công thức nghiệm phương trình bậc hai tổng quát và ví dụ áp dụng?");
        AddBotMsg(4, 248,
            "### Hệ thống Công thức Định lý Master & Phương trình Đặc trưng Bậc 2\n\n" +
            "#### 1. Nghiệm Tổng quát Phương trình Đặc trưng $a r^2 + b r + c = 0$\n" +
            "$$r_{1,2} = \\frac{-b \\pm \\sqrt{b^2 - 4ac}}{2a}$$\n" +
            "- Nếu $\\Delta = b^2 - 4ac > 0$ ($r_1 \\ne r_2$): $u_n = \\alpha r_1^n + \\beta r_2^n$.\n" +
            "- Nếu $\\Delta = 0$ (nghiệm kép $r_0$): $u_n = (\\alpha + \\beta n) r_0^n$.\n\n" +
            "#### 2. Ba Trường hợp Định lý Master cho $T(n) = a T(n/b) + \\Theta(n^d)$ ($a \\ge 1, b > 1, d \\ge 0$)\n\n" +
            "| So sánh $d$ và $\\log_b a$ | Độ phức tạp $T(n)$ | Thuật toán Điển hình |\n" +
            "| :--- | :--- | :--- |\n" +
            "| $d < \\log_b a$ | $\\Theta(n^{\\log_b a})$ | Nhân ma trận Strassen ($T(n)=7T(n/2)+\\Theta(n^2) \\Rightarrow \\Theta(n^{\\log_2 7})$) |\n" +
            "| $d = \\log_b a$ | $\\Theta(n^d \\log n)$ | Merge Sort ($T(n)=2T(n/2)+\\Theta(n) \\Rightarrow \\Theta(n \\log n)$) |\n" +
            "| $d > \\log_b a$ | $\\Theta(n^d)$ | Xây dựng Binary Heap tuyến tính |",
            pinned: true);

        AddStudentMsg(4, 10, 225, "Công thức nghiệm bậc 2 với bảng Master Theorem nhìn nét căng không lệch 1 pixel nào luôn!");
        AddStudentMsg(4, 4, 200, "Mình ghim lại ngay rồi, mai thi chỉ cần mở thanh Tin nhắn đã ghim ra nhẩm lại 30 giây là thuộc.");
        AddStudentMsg(4, 9, 175, "Cảm ơn Huy với @bot nha, mình vào làm đề Quiz MAT108 đây!");

        // ==================== PHÒNG 6: OS209 (Hệ điều hành Linux - Active) ====================
        AddStudentMsg(5, 7, 190, "Nhóm ôn Hệ điều hành Linux đã mở công khai! Mã mời #OS209, các bạn gửi cho nhóm lớp vào làm đề nhé.");
        AddStudentMsg(5, 10, 185, "@bot Tóm tắt 4 điều kiện Coffman gây Deadlock và cách ngăn chặn trong Hệ điều hành Linux?");
        AddBotMsg(5, 184,
            "### 4 Điều kiện Coffman Gây `Deadlock` & Chiến lược Ngăn chặn\n\n" +
            "Tắc nghẽn (*Deadlock*) chỉ xảy ra khi **cả 4 điều kiện** dưới đây cùng đồng thời thỏa mãn:\n\n" +
            "1. **Mutual Exclusion (*Loại trừ tương hỗ*):** Tài nguyên găng không thể chia sẻ đồng thời.\n" +
            "2. **Hold and Wait (*Giữ và Chờ*):** Tiến trình đang nắm giữ ít nhất 1 tài nguyên và yêu cầu thêm tài nguyên đang bị tiến trình khác giữ.\n" +
            "3. **No Preemption (*Không trưng dụng*):** Không thể cưỡng bức thu hồi tài nguyên trước khi tiến trình tự giải phóng.\n" +
            "4. **Circular Wait (*Chờ đợi vòng tròn*):** Tồn tại chu trình khép kín $P_0 \\to P_1 \\to \\dots \\to P_k \\to P_0$.\n\n" +
            "> **Giải pháp thực tế trong nhân Linux:** Đánh số thứ tự toàn cục cho các khóa (`Mutex Lock Ordering`) để triệt tiêu hoàn toàn điều kiện số 4 (*Circular Wait*).",
            pinned: true);

        // ==================== PHÒNG 7: WEB301 (Phát triển Ứng dụng Web & RESTful API) ====================
        AddStudentMsg(6, 2, 175, "Chào mừng anh em vào phòng #WEB301! Giáo trình thiết kế RESTful API, xác thực JWT và tối ưu HTTP/2 đã lên kệ.");
        AddStudentMsg(6, 1, 170, "@bot Phân biệt tính Lũy đẳng (Idempotent) giữa các HTTP Methods GET, POST, PUT, DELETE trong thiết kế RESTful API?");
        AddBotMsg(6, 169,
            "### Tính Lũy đẳng (*Idempotency*) trong Kiến trúc RESTful API\n\n" +
            "Một thao tác HTTP được gọi là **Idempotent** nếu việc thực thi nó $N$ lần ($N \\ge 1$) với cùng tham số mang lại **trạng thái tài nguyên trên máy chủ giống hệt** như thực thi $1$ lần duy nhất:\n\n" +
            "| Phương thức | An toàn (*Safe*) | Lũy đẳng (*Idempotent*) | Giải thích thực tế |\n" +
            "| :--- | :---: | :---: | :--- |\n" +
            "| `GET` | Có | **Có** | Chỉ đọc dữ liệu, không làm biến đổi trạng thái máy chủ |\n" +
            "| `PUT` | Không | **Có** | Ghi đè toàn bộ tài nguyên tại URI xác định bằng trạng thái mới |\n" +
            "| `DELETE` | Không | **Có** | Lần đầu xóa trả `200/204`, các lần sau tài nguyên vẫn ở trạng thái đã xóa |\n" +
            "| `POST` | Không | **Không** | Mỗi lần gọi tạo ra một bản ghi mới với khóa định danh mới |",
            pinned: true);

        // ==================== PHÒNG 8: SEC308 (An toàn Bảo mật Thông tin) ====================
        AddStudentMsg(7, 3, 160, "Phòng #SEC308 chuyên ôn Mật mã học RSA, băm mật khẩu BCrypt và phòng chống lỗ hổng OWASP Top 10 đây nhé!");
        AddStudentMsg(7, 5, 155, "@bot Trình bày các bước toán học sinh cặp khóa Công khai / Bí mật của hệ mật mã RSA kèm ví dụ tính toán cụ thể?");
        AddBotMsg(7, 154,
            "### Quy trình Sinh Khóa & Mã hóa Hệ Bất đối xứng `RSA`\n\n" +
            "1. Chọn hai số nguyên tố lớn phân biệt $p$ và $q$, tính mô-đun công khai $n = p \\cdot q$.\n" +
            "2. Tính phi hàm Euler: $$\\varphi(n) = (p - 1)(q - 1)$$\n" +
            "3. Chọn số mũ công khai $e$ sao cho $1 < e < \\varphi(n)$ và $\\gcd(e, \\varphi(n)) = 1$.\n" +
            "4. Tìm số mũ bí mật $d$ bằng thuật toán Euclid mở rộng thỏa mãn: $$e \\cdot d \\equiv 1 \\pmod{\\varphi(n)}$$\n" +
            "5. **Mã hóa bản rõ $M$:** $C = M^e \\bmod n$ $\\quad|\\quad$ **Giải mã bản mã $C$:** $M = C^d \\bmod n$.",
            pinned: true);

        // ==================== PHÒNG 9: CLD405 (Điện toán Đám mây & Docker CI/CD) ====================
        AddStudentMsg(8, 8, 145, "Team Cloud & DevOps ơi, mình vừa đẩy tài liệu Multi-Stage Dockerfile và chiến lược Blue-Green Deployment lên phòng #CLD405!");
        AddStudentMsg(8, 6, 140, "@bot Viết một Multi-Stage Dockerfile mẫu tối ưu dung lượng cho ứng dụng Web API .NET 8?");
        AddBotMsg(8, 139,
            "### Mẫu `Multi-Stage Dockerfile` Tối ưu cho ASP.NET Core 8\n\n" +
            "```dockerfile\n" +
            "# Stage 1: Build & Publish với bộ SDK đầy đủ\n" +
            "FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build\n" +
            "WORKDIR /src\n" +
            "COPY . .\n" +
            "RUN dotnet publish src/SmartStudy.API/SmartStudy.API.csproj -c Release -o /app/publish\n\n" +
            "# Stage 2: Ảnh Runtime siêu nhẹ chỉ chứa môi trường thực thi\n" +
            "FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime\n" +
            "WORKDIR /app\n" +
            "COPY --from=build /app/publish .\n" +
            "EXPOSE 5000\n" +
            "ENTRYPOINT [\"dotnet\", \"SmartStudy.API.dll\"]\n" +
            "```\n\n" +
            "- Loại bỏ hoàn toàn trình biên dịch SDK khỏi ảnh chạy thực tế, giảm dung lượng từ `~820 MB` xuống chỉ còn **`~105 MB`**.",
            pinned: true);

        // ==================== PHÒNG 10: SWE206 (Công nghệ Phần mềm & Clean Architecture) ====================
        AddStudentMsg(9, 9, 130, "Phòng #SWE206 ôn thi Công nghệ Phần mềm: 5 nguyên lý SOLID, Clean Architecture 4 tầng và viết Unit Test với xUnit!");
        AddStudentMsg(9, 0, 125, "@bot Tóm tắt ngắn gọn 5 nguyên lý SOLID kèm ví dụ thực tế dễ nhớ nhất khi vấn đáp?");
        AddBotMsg(9, 124,
            "### Bảng Tóm tắt 5 Nguyên lý Thiết kế Hướng đối tượng `SOLID`\n\n" +
            "| Ký hiệu | Nguyên lý | Ý nghĩa Cốt lõi khi Thiết kế |\n" +
            "| :---: | :--- | :--- |\n" +
            "| **S** | *Single Responsibility* | Mỗi class chỉ đảm nhiệm đúng 1 trách nhiệm nghiệp vụ duy nhất |\n" +
            "| **O** | *Open / Closed* | Mở để mở rộng tính năng mới qua Interface, đóng việc sửa code cũ |\n" +
            "| **L** | *Liskov Substitution* | Lớp dẫn xuất thay thế hoàn toàn được lớp cơ sở mà không phá vỡ tính đúng đắn |\n" +
            "| **I** | *Interface Segregation* | Tách thành các Interface nhỏ chuyên biệt thay vì 1 Interface khổng lồ |\n" +
            "| **D** | *Dependency Inversion* | Tầng nghiệp vụ phụ thuộc vào `Interface` trừu tượng, không phụ thuộc hạ tầng |",
            pinned: true);

        // ==================== PHÒNG 11: IOT310 (Hệ thống Nhúng IoT - Pending chờ Admin duyệt) ====================
        AddStudentMsg(10, 10, 110, "Phòng nghiên cứu IoT ESP32 & MQTT (#IOT310) vừa khởi tạo, đang chờ Quản trị viên duyệt hiển thị ngoài sảnh chung!");
        AddStudentMsg(10, 4, 105, "@bot Phân biệt 3 mức Chất lượng Dịch vụ QoS 0, QoS 1 và QoS 2 trong giao thức truyền thông IoT MQTT?");
        AddBotMsg(10, 104,
            "### Ba Mức Chất lượng Dịch vụ (`QoS`) trong Giao thức `MQTT`\n\n" +
            "- **`QoS 0` (*At most once — Fire & Forget*):** Gửi tối đa 1 lần không cần gói xác nhận `PUBACK`, nhanh nhất cho dữ liệu nhiệt độ chu kỳ ngắn.\n" +
            "- **`QoS 1` (*At least once*):** Đảm bảo gói tin tới đích ít nhất 1 lần nhờ cơ chế xác nhận `PUBACK` (có thể trùng lặp bản tin).\n" +
            "- **`QoS 2` (*Exactly once*):** Bắt tay 4 bước (`PUBLISH` $\\to$ `PUBREC` $\\to$ `PUBREL` $\\to$ `PUBCOMP`) đảm bảo nhận **đúng 1 lần duy nhất**, dùng cho lệnh điều khiển khóa cửa / thanh toán.",
            pinned: true);

        return messages;
    }
}
