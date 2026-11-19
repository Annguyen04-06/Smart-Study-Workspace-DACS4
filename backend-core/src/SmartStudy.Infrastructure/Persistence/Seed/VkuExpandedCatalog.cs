using SmartStudy.Domain.Entities;
using SmartStudy.Domain.Enums;

namespace SmartStudy.Infrastructure.Persistence.Seed;

/// <summary>
/// Bộ dữ liệu mở rộng bổ sung thêm 9 sinh viên VKU (23IT012..23IT020) và 11 phòng học chuyên ngành mới
/// (nâng tổng quy mô lên 22 phòng học: 20 Phòng Active mở công khai + 2 Phòng Pending chờ Admin duyệt,
/// 24 tệp PDF vật lý, 20 đề Quiz, 20 bộ thẻ 3D, >260 tin nhắn thảo luận và >90 lượt thi Leaderboard).
/// </summary>
internal static class VkuExpandedCatalog
{
    internal sealed record ExpandedRoomSeedSpec(
        string InviteCode,
        string Name,
        string SubjectCategory,
        string Description,
        RoomStatus Status,
        int DaysAgo,
        int[] MemberStudentIndices,
        string PdfFileName,
        string PdfTitle,
        int LogicalPages,
        string[] PdfSections,
        string AiSummary,
        string BotQuestion,
        string BotPinnedMarkdown,
        string QuizTitle,
        QuizDifficulty Difficulty,
        (string Q, string A, string B, string C, string D, string Ans, string Exp)[] Questions,
        string DeckTitle,
        (string Front, string Back, int Mastery)[] Cards);

    public static (string Code, string Name, string Email, string SeedName)[] GetExtraStudents() =>
    [
        ("23IT012", "Đỗ Quỳnh Hương",   "huong.dq.23it@vku.udn.vn", "DoQuynhHuong"),
        ("23IT013", "Lý Minh Khôi",     "khoi.lm.23it@vku.udn.vn",  "LyMinhKhoi"),
        ("23IT014", "Mai Phương Thảo",  "thao.mp.23it@vku.udn.vn",  "MaiPhuongThao"),
        ("23IT015", "Hồ Trọng Nghĩa",   "nghia.ht.23it@vku.udn.vn", "HoTrongNghia"),
        ("23IT016", "Đinh Bảo Ngọc",    "ngoc.db.23it@vku.udn.vn",  "DinhBaoNgoc"),
        ("23IT017", "Vũ Hoàng Phúc",    "phuc.vh.23it@vku.udn.vn",  "VuHoangPhuc"),
        ("23IT018", "Tạ Thanh Sơn",     "son.tt.23it@vku.udn.vn",   "TaThanhSon"),
        ("23IT019", "Lương Yến Nhi",    "nhi.ly.23it@vku.udn.vn",   "LuongYenNhi"),
        ("23IT020", "Châu Gia Kiệt",    "kiet.cg.23it@vku.udn.vn",  "ChauGiaKiet")
    ];

    public static List<ExpandedRoomSeedSpec> GetExpandedRooms() =>
    [
        // 12. MOB302 (Active)
        new(
            "MOB302",
            "Lập trình Thiết bị Di động Đa nền tảng với Flutter & Dart",
            "Lập trình Di động",
            "Kiến trúc Widget Tree, Element Tree, RenderObject, quản lý trạng thái BLoC/Riverpod và tối ưu 60fps/120fps trên iOS & Android.",
            RoomStatus.Active, 12,
            [11, 0, 2, 5, 12, 14, 16, 18],
            "vku-mob302-flutter-state-architecture.pdf",
            "Giáo trình Flutter Chuyên sâu: Cơ chế Render 3 Cây, Isolates Bất đồng bộ & Quản lý State",
            22,
            [
                "## SECTION 1: Ba Cay Kien truc Cot loi trong Flutter (Widget - Element - RenderObject)",
                "Trong Flutter, Widget chi la ban thiet ke bat bien (Immutable Configuration) sieu nhe. Framework duy tri Element Tree lam cau noi giu trang thai va chi cap nhat RenderObject Tree khi loai hoac thuoc tinh Widget thuc su thay doi, dat toc do ket xuat 60-120 khung hinh/giay qua engine Impeller.",
                "## SECTION 2: Event Loop, Future, Stream va Da luong voi Dart Isolates",
                "Dart van hanh tren mo hinh Single-Threaded Event Loop gom Microtask Queue va Event Queue. De xu ly tac vu nang nhu giai ma JSON lon hay xu ly anh ma khong gay giat khung hinh (UI Jank), ta su dung Isolate.run() voi bo nho co lap khong chia se (No Shared Memory) tranh Race Condition."
            ],
            "### Tóm tắt Học thuật: Kiến trúc Flutter 3 Cây & Dart Isolates\n\n" +
            "| Thành phần | Đặc tính Bộ nhớ | Vai trò Kiến trúc |\n" +
            "| :--- | :--- | :--- |\n" +
            "| **Widget Tree** | Bất biến (*Immutable*), tạo lại liên tục | Mô tả cấu hình giao diện |\n" +
            "| **Element Tree** | Khả biến (*Mutable*), giữ `State` | Đối chiếu (*Diffing*) và quản lý vòng đời |\n" +
            "| **RenderObject** | Nặng, tái sử dụng tối đa | Tính toán *Layout* và vẽ *Paint* lên GPU |\n\n" +
            "- **Ngân sách khung hình 60fps:** Mỗi khung hình phải hoàn tất Layout + Paint dưới $\\frac{1000}{60} \\approx 16.67\\text{ ms}$.",
            "@bot Giải thích vì sao xây dựng lại Widget liên tục trong Flutter không làm chậm ứng dụng và khi nào cần dùng Dart Isolate?",
            "### Cơ chế Tối ưu `Widget Rebuild` & Đa luồng `Isolate` trong Flutter\n\n" +
            "1. **Vì sao tạo lại `Widget` rất nhanh?** `Widget` chỉ là đối tượng cấu hình bất biến siêu nhẹ; Flutter đối chiếu `runtimeType` và `key` trên **Element Tree** để giữ nguyên **RenderObject** tốn kém bên dưới.\n" +
            "2. **Khi nào dùng `Isolate.run()`?** Khi tác vụ CPU kéo dài quá **$16.67\\text{ ms}$** (ngưỡng rớt khung hình 60fps), ví dụ bóc tách tệp JSON hàng chục MB.",
            "Kiểm tra Kiến trúc Flutter 3 Cây, BLoC & Dart Isolates",
            QuizDifficulty.Medium,
            [
                ("Trong kiến trúc 3 cây của Flutter, cây nào trực tiếp thực hiện tính toán kích thước hình học (Layout) và vẽ lên màn hình (Paint)?", "Widget Tree", "RenderObject Tree", "Asset Bundle Tree", "Package Tree", "B", "Widget chỉ giữ cấu hình bất biến; RenderObject mới là đối tượng nặng tính toán Layout constraints và thực thi Paint."),
                ("Để đảm bảo giao diện di động mượt mà ở tốc độ 60 FPS, mỗi khung hình phải hoàn tất xử lý trong tối đa bao nhiêu mili-giây?", " khoảng 16.67 ms", "100 ms", "60 ms", "500 ms", "A", "Ta có 1000 ms / 60 khung hình = 16.67 ms cho mỗi chu kỳ Build + Layout + Paint."),
                ("Đặc trưng bộ nhớ quan trọng nhất của các luồng Dart Isolates là gì?", "Dùng chung toàn bộ biến toàn cục không cần khóa", "Mỗi Isolate có vùng nhớ Heap riêng biệt, giao tiếp qua truyền thông điệp nên không xảy ra Race Condition bộ nhớ", "Chỉ chạy được trên iOS", "Tự động lưu vào SQLite", "B", "Không chia sẻ bộ nhớ (Isolated Heap) giúp việc thu gom rác (GC) cực nhanh mà không cần khóa toàn bộ tiến trình."),
                ("Từ khóa const đặt trước Constructor của Widget trong Flutter mang lại lợi ích gì?", "Tự động gọi API", "Tạo hằng đối tượng tại thời điểm biên dịch giúp Flutter bỏ qua hoàn toàn bước rebuild nhánh Widget con đó", "Tăng gấp đôi cỡ chữ", "Mã hóa dữ liệu mạng", "B", "Const Widget dùng lại cùng một tham chiếu vùng nhớ nên Element đánh dấu không cần cập nhật lại."),
                ("Trong vòng lặp sự kiện (Event Loop) của Dart, hàng đợi nào được ưu tiên xử lý hết trước khi lấy tác vụ từ Event Queue?", "Timer Queue", "Microtask Queue", "Network Socket Queue", "Disk I/O Queue", "B", "Sau mỗi lượt thực thi, Dart luôn rút cạn toàn bộ Microtask Queue trước khi xử lý phần tử tiếp theo của Event Queue.")
            ],
            "Bộ thẻ Flutter 3-Tree Architecture & Dart Async",
            [
                ("Sự khác biệt giữa StatelessWidget và StatefulWidget?", "StatelessWidget không lưu trạng thái nội tại thay đổi theo thời gian; StatefulWidget gắn liền với một đối tượng State bền vững trên Element Tree.", 2),
                ("Quy tắc Layout cốt lõi của Flutter là gì?", "Constraints go down, Sizes go up, Parent sets position (Ràng buộc truyền xuống, Kích thước báo lên, Cha đặt tọa độ).", 2),
                ("Khi nào cần truyền Key (ValueKey / ObjectKey) cho danh sách Widget?", "Khi thêm, xóa hoặc đảo thứ tự các StatefulWidget cùng kiểu trong danh sách để Element ghép đúng State tương ứng.", 1),
                ("Khác biệt giữa Future<T> và Stream<T> trong Dart?", "Future trả về duy nhất 1 kết quả bất đồng bộ trong tương lai; Stream phát ra chuỗi nhiều sự kiện liên tiếp theo thời gian.", 2),
                ("Engine kết xuất đồ họa mới mặc định của Flutter giúp triệt tiêu Shader Compilation Jank tên là gì?", "Impeller Engine (biên dịch trước toàn bộ Shaders lúc đóng gói ứng dụng).", 1)
            ]),

        // 13. DLG401 (Active)
        new(
            "DLG401",
            "Học sâu (Deep Learning), Mạng Nơ-ron Tích chập CNN & Transformer",
            "Học sâu & Thị giác máy",
            "Lan truyền ngược Backpropagation, hàm kích hoạt ReLU/GELU, Batch Normalization, kiến trúc ResNet Skip-Connection và cơ chế Self-Attention.",
            RoomStatus.Active, 11,
            [12, 1, 4, 8, 11, 13, 15, 19],
            "vku-dlg401-cnn-transformer-attention.pdf",
            "Chuyên đề Học sâu: Lan truyền Ngược, Tích chập CNN, ResNet & Cơ chế Self-Attention",
            25,
            [
                "## SECTION 1: Van de Triet tieu Dao ham (Vanishing Gradient) va ResNet Skip Connection",
                "Trong mang no-ron nhieu tang dung ham Sigmoid/Tanh, dao ham nho hon 0.25 nhan don qua nhieu lop lam tin hieu cap nhat o cac lop dau tien ve 0. Viec ket hop ham ReLU(x) = max(0, x), khoi tao He Initialization va duong tat Cong du (Residual Connection y = F(x) + x) cho phep huan luyen mang sau hang tram tang.",
                "## SECTION 2: Tinh kich thuoc Feature Map trong CNN va Co che Scaled Dot-Product Attention",
                "Voi anh dau vao W x W, bo loc K x K, dem vien Padding P va buoc nhay Stride S, kich thuoc dau ra la floor((W - K + 2P) / S) + 1. Trong kien truc Transformer, Scaled Dot-Product Attention tinh trong so chu y qua Softmax((Q * K^T) / sqrt(d_k)) * V."
            ],
            "### Tóm tắt Học thuật: Mạng CNN & Cơ chế Self-Attention\n" +
            "- **Kích thước không gian đầu ra tầng Tích chập (*Conv2D*):**\n" +
            "$$W_{\\text{out}} = \\left\\lfloor \\frac{W_{\\text{in}} - K + 2P}{S} \\right\\rfloor + 1$$\n" +
            "- **Công thức *Scaled Dot-Product Attention* trong Transformer:**\n" +
            "$$\\text{Attention}(Q, K, V) = \\text{softmax}\\!\\left(\\frac{QK^T}{\\sqrt{d_k}}\\right)V$$",
            "@bot Viết công thức tính kích thước ảnh đầu ra sau tầng tích chập Conv2D và giải thích vai trò của mẫu số căn bậc hai d_k trong Self-Attention?",
            "### Công thức `Conv2D Output` & Chuẩn hóa $\\sqrt{d_k}$ trong `Transformer`\n\n" +
            "1. **Kích thước bản đồ đặc trưng (*Feature Map*):** Với ảnh $W \\times W$, kernel $K$, đệm $P$, bước trượt $S$:\n" +
            "   $$W_{\\text{out}} = \\left\\lfloor \\frac{W - K + 2P}{S} \\right\\rfloor + 1$$\n" +
            "   *(Ví dụ: $W=32, K=3, P=1, S=1 \\Rightarrow W_{\\text{out}} = 32$ giữ nguyên kích thước).* \n" +
            "2. **Vì sao chia cho $\\sqrt{d_k}$?** Khi số chiều $d_k$ lớn, tích vô hướng $Q K^T$ có phương sai bằng $d_k$ làm hàm `softmax` rơi vào vùng bão hòa có đạo hàm cực nhỏ ($\\approx 0$). Chia cho $\\sqrt{d_k}$ đưa phương sai về $1$, giữ đạo hàm ổn định.",
            "Đánh giá Học sâu: Tích chập CNN, ResNet & Kiến trúc Transformer",
            QuizDifficulty.Hard,
            [
                ("Cho ảnh đầu vào kích thước 64x64 đi qua tầng Conv2D có Kernel 3x3, Padding P = 1 và Stride S = 2. Kích thước không gian đầu ra là?", "64x64", "32x32", "31x31", "62x62", "B", "Áp dụng công thức floor((64 - 3 + 2*1) / 2) + 1 = floor(63 / 2) + 1 = 31 + 1 = 32."),
                ("Đường nối tắt Cộng dư (Residual / Skip Connection: y = F(x) + x) trong mạng ResNet giúp giải quyết triệt để hiện tượng nào?", "Thiếu dung lượng ổ cứng", "Suy giảm hiệu năng và triệt tiêu đạo hàm (Vanishing Gradient) khi mạng quá sâu", "Mất kết nối mạng", "Lỗi thiếu nhãn dữ liệu", "B", "Đạo hàm truyền ngược qua nhánh cộng trực tiếp (+x) có hệ số bằng 1 nên thông tin gradient đi thẳng về các tầng đầu mà không bị suy hao."),
                ("Trong công thức Scaled Dot-Product Attention, phép chia tích vô hướng Q*K^T cho sqrt(d_k) có tác dụng gì?", "Tăng gấp đôi số tham số", "Giữ phương sai ổn định quanh 1 để ngăn hàm Softmax bị bão hòa gây triệt tiêu đạo hàm", "Loại bỏ ma trận V", "Chuyển ảnh màu sang ảnh xám", "B", "Nếu không chia sqrt(d_k), giá trị logit quá lớn đẩy xác suất Softmax về sát 0 hoặc 1 làm gradient gần bằng 0."),
                ("Kỹ thuật Dropout trong quá trình huấn luyện mạng nơ-ron hoạt động như thế nào?", "Tắt ngẫu nhiên một tỷ lệ p các nơ-ron ở mỗi bước huấn luyện để chống đồng thích nghi và giảm Overfitting", "Xóa vĩnh viễn 50% ảnh huấn luyện", "Tăng tốc độ học lên 10 lần", "Chỉ dùng lúc suy diễn (Inference)", "A", "Dropout buộc mạng học các biểu diễn dư thừa bền vững như một tập hợp (Ensemble) nhiều mạng con."),
                ("Ưu điểm vượt trội của kiến trúc Transformer so với mạng hồi quy tuần tự RNN/LSTM khi huấn luyện trên GPU là?", "Không cần hàm kích hoạt", "Tính toán song song toàn bộ các vị trí từ trong câu cùng lúc nhờ cơ chế Self-Attention thay vì phải chờ từng bước thời gian t-1", "Không dùng phép nhân ma trận", "Chỉ cần 10 mẫu dữ liệu", "B", "RNN phụ thuộc trạng thái tuần tự h_{t-1}, còn Transformer xử lý toàn chuỗi bằng nhân ma trận song song cực nhanh trên Tensor Cores.")
            ],
            "Bộ thẻ Deep Learning: CNN, ResNet & Transformer",
            [
                ("Công thức hàm kích hoạt ReLU và ưu điểm chính?", "ReLU(x) = max(0, x); tính toán cực nhanh và đạo hàm bằng 1 với mọi x > 0 giúp giảm Vanishing Gradient.", 2),
                ("Công thức tính kích thước đầu ra tầng Conv2D?", "W_out = floor((W_in - K + 2P) / S) + 1.", 2),
                ("Vai trò của tầng Pooling (MaxPool / AvgPool) trong CNN?", "Giảm kích thước không gian đặc trưng, giảm chi phí tính toán và tăng tính bất biến dịch chuyển nhỏ.", 1),
                ("Công thức cốt lõi của Scaled Dot-Product Attention?", "Attention(Q, K, V) = softmax((Q * K^T) / sqrt(d_k)) * V.", 2),
                ("Khác biệt chế độ model.train() và model.eval() trong PyTorch?", "Ở chế độ eval(), Dropout được tắt hoàn toàn và BatchNorm sử dụng trung bình/phương sai trượt tích lũy thay vì thống kê theo batch hiện tại.", 1)
            ]),

        // 14. NLP403 (Active)
        new(
            "NLP403",
            "Xử lý Ngôn ngữ Tự nhiên (NLP), Mô hình Ngôn ngữ Lớn LLM & Kiến trúc RAG",
            "Trí tuệ nhân tạo",
            "Tokenization BPE, nhúng từ Vector Embeddings, độ tương đồng Cosine Similarity, tìm kiếm ngữ nghĩa Vector DB và chống ảo giác với RAG.",
            RoomStatus.Active, 10,
            [13, 2, 4, 9, 12, 14, 17, 19],
            "vku-nlp403-llm-embeddings-rag.pdf",
            "Xử lý Ngôn ngữ Tự nhiên Hiện đại: Byte-Pair Encoding, Vector Embeddings & Hệ thống RAG",
            21,
            [
                "## SECTION 1: Thuat toan Tach tu phu Byte-Pair Encoding (BPE) va Khong gian Vector Nhung",
                "Cac mo hinh ngon ngu lon (LLM) khong tach tu theo khoang trang don thuan ma dung Subword Tokenization nhu BPE hoac SentencePiece de xu ly tu hiem va da ngon ngu nhu tieng Viet ma khong bi loi Out-of-Vocabulary (OOV).",
                "## SECTION 2: Do tuong dong Cosine va Kien truc Retrieval-Augmented Generation (RAG)",
                "Moi doan van ban (Chunk) duoc anh xa thanh vector nhieu chieu u va v. Do gan gui ngu nghia duoc do bang Cosine Similarity: cos(theta) = (u . v) / (||u|| * ||v||). Kien truc RAG truy xuat Top-K doan tai lieu lien quan nhat dua vao Prompt giup triet tieu hien tuong Ao giac (Hallucination)."
            ],
            "### Tóm tắt Học thuật: Vector Embeddings & Kiến trúc RAG\n" +
            "- **Độ tương đồng ngữ nghĩa *Cosine Similarity*:**\n" +
            "$$\\text{sim}(\\mathbf{u}, \\mathbf{v}) = \\cos(\\theta) = \\frac{\\mathbf{u} \\cdot \\mathbf{v}}{\\|\\mathbf{u}\\|_2 \\, \\|\\mathbf{v}\\|_2} = \\frac{\\sum_{i=1}^{d} u_i v_i}{\\sqrt{\\sum_{i=1}^{d} u_i^2}\\sqrt{\\sum_{i=1}^{d} v_i^2}}$$\n" +
            "- **Chu trình RAG 3 pha:** *Chunking & Embedding* $\\longrightarrow$ *Top-$K$ Semantic Retrieval* $\\longrightarrow$ *Grounded Generation*.",
            "@bot Trình bày công thức tính độ tương đồng Cosine giữa hai vector ngữ nghĩa và giải thích vì sao RAG giảm thiểu ảo giác cho LLM?",
            "### Độ tương đồng `Cosine Similarity` & Chống Ảo giác bằng `RAG`\n\n" +
            "$$\\cos(\\theta) = \\frac{\\mathbf{u} \\cdot \\mathbf{v}}{\\|\\mathbf{u}\\|_2 \\cdot \\|\\mathbf{v}\\|_2} \\in [-1, 1]$$\n\n" +
            "- Khác với khoảng cách Euclid nhạy cảm với độ dài văn bản, **Cosine Similarity** chỉ đo góc định hướng ngữ nghĩa trong không gian nhiều chiều.\n" +
            "- **RAG (*Retrieval-Augmented Generation*)** cung cấp bằng chứng học liệu trực tiếp vào ngữ cảnh đầu vào, buộc mô hình chỉ tổng hợp dựa trên nguồn xác thực thay vì tự suy đoán.",
            "Trắc nghiệm NLP, Subword Tokenization & Kiến trúc RAG",
            QuizDifficulty.Medium,
            [
                ("Ưu điểm lớn nhất của thuật toán tách từ phụ Byte-Pair Encoding (BPE) so với từ điển từ nguyên vẹn truyền thống là?", "Không cần bộ nhớ RAM", "Giải quyết triệt để từ mới chưa gặp (Out-of-Vocabulary) bằng cách ghép từ các mảnh ký tự/từ phụ phổ biến", "Chỉ hỗ trợ tiếng Anh", "Bỏ qua toàn bộ dấu câu", "B", "BPE giữ nguyên các từ phổ biến thành 1 token và tách từ hiếm thành các subwords đã biết."),
                ("Giá trị của độ tương đồng Cosine giữa hai vector nhúng cùng hướng hoàn toàn bằng bao nhiêu?", "0", "1", "-1", "Vô cùng", "B", "Khi góc giữa hai vector bằng 0 độ, cos(0) = 1 biểu thị mức tương đồng ngữ nghĩa cực đại."),
                ("Mục tiêu cốt lõi nhất của kiến trúc Retrieval-Augmented Generation (RAG) là gì?", "Nén kích thước tệp ảnh", "Kết hợp truy xuất tài liệu chuẩn xác bên ngoài vào ngữ cảnh để cập nhật tri thức mới và giảm ảo giác (Hallucination)", "Thay thế hoàn toàn CSDL quan hệ", "Tắt bước Tokenization", "B", "RAG cho phép trợ giảng AI trả lời bám sát giáo trình riêng của từng phòng học mà không cần huấn luyện lại trọng số LLM."),
                ("Kỹ thuật chia văn bản PDF dài thành các đoạn chồng lấp nhỏ (Chunking with Overlap) trong RAG nhằm tránh rủi ro nào?", "Tràn ổ cứng", "Cắt đứt câu hoặc làm mất mạch ngữ cảnh nằm ngay tại ranh giới giữa hai đoạn liên tiếp", "Tăng độ trễ mạng LAN", "Sai định dạng phông chữ", "B", "Phần chồng lấp (ví dụ 10-15% số từ) bảo toàn trọn vẹn ý nghĩa của các mệnh đề nằm giữa hai đoạn."),
                ("Tham số Temperature trong quá trình sinh văn bản của mô hình ngôn ngữ lớn điều chỉnh yếu tố nào?", "Nhiệt độ vật lý của GPU", "Mức độ ngẫu nhiên / sáng tạo của phân phối xác suất từ tiếp theo (Temperature thấp giúp câu trả lời nhất quán, chuẩn xác hơn)", "Kích thước cửa sổ ngữ cảnh", "Tốc độ đường truyền mạng", "B", "Trong các tác vụ học thuật và trích xuất JSON có cấu trúc, Temperature thấp (0.1 - 0.3) giúp đầu ra ổn định và chính xác.")
            ],
            "Bộ thẻ NLP, Vector Embeddings & RAG",
            [
                ("BPE (Byte-Pair Encoding) hoạt động theo nguyên tắc nào?", "Khởi tạo từ tập ký tự cơ sở và lặp lại việc gộp cặp token liền kề xuất hiện nhiều nhất thành token mới.", 2),
                ("Công thức Cosine Similarity giữa hai vector u và v?", "cos(theta) = (u . v) / (||u|| * ||v||), nhận giá trị trong đoạn [-1, 1].", 2),
                ("Ba bước chính trong đường ống truy vấn RAG?", "1. Nhúng câu hỏi thành Vector -> 2. Tìm Top-K đoạn học liệu gần nhất -> 3. Ghép học liệu vào Prompt để sinh câu trả lời.", 2),
                ("Hiện tượng Hallucination (Ảo giác) của LLM là gì?", "Mô hình sinh ra nội dung nghe rất trôi chảy tự tin nhưng sai lệch sự thật hoặc không có trong tài liệu gốc.", 1),
                ("Structured Output (JSON Schema) giúp ích gì khi tích hợp AI vào Backend?", "Ép bộ giải mã tuân thủ 100% lược đồ kiểu dữ liệu định trước, không bao giờ lỗi cú pháp JSON khi parse sang DTO.", 2)
            ]),

        // 15. BIG309 (Active)
        new(
            "BIG309",
            "Phân tích Dữ liệu Lớn (Big Data) với Apache Spark & Luồng sự kiện Kafka",
            "Khoa học Dữ liệu",
            "Kiến trúc xử lý phân tán MapReduce vs Spark RDD/DataFrame trong RAM, Lazy Evaluation, DAG Scheduler và truyền thông điệp Apache Kafka.",
            RoomStatus.Active, 9,
            [14, 3, 6, 10, 12, 15, 18],
            "vku-big309-spark-kafka-streaming.pdf",
            "Hệ sinh thái Dữ liệu Lớn: Xử lý Phân tán Apache Spark trong RAM & Kiến trúc Apache Kafka",
            20,
            [
                "## SECTION 1: Tai sao Apache Spark Nhanh gap hang chuc lan Hadoop MapReduce?",
                "Hadoop MapReduce phai ghi ket qua trung gian xuong o dia HDFS sau moi pha Map va Reduce. Nguoc lai, Apache Spark giu du lieu phan tan ngay trong bo nho RAM qua Spark DataFrame / Catalyst Optimizer va chi thuc thi khi gap Action nho co che Lazy Evaluation.",
                "## SECTION 2: Phan vung Chu de (Topic Partitions) va Consumer Group trong Apache Kafka",
                "Trong Kafka, moi Topic duoc chia thanh nhieu Partitions ghi noi tiep bat bien (Append-only Commit Log) cho thong luong hang trieu su kien/giay. Trong cung mot Consumer Group, moi Partition chi duoc gan cho toi da 1 Consumer de dam bao thu tu xu ly."
            ],
            "### Tóm tắt Học thuật: Apache Spark & Apache Kafka\n\n" +
            "| Đặc điểm | Transformation (*Spark*) | Action (*Spark*) |\n" +
            "| :--- | :--- | :--- |\n" +
            "| **Cơ chế thực thi** | *Lazy* (chỉ ghi nhận vào đồ thị `DAG`) | *Eager* (kích hoạt chạy Job trên Cluster) |\n" +
            "| **Ví dụ hàm** | `select()`, `filter()`, `groupBy()`, `join()` | `count()`, `collect()`, `write.parquet()` |\n\n" +
            "- **Quy tắc song song Kafka:** Số `Consumers` hoạt động hiệu quả tối đa trong 1 *Consumer Group* bằng tổng số `Partitions` của Topic.",
            "@bot Phân biệt Transformation và Action trong Apache Spark kèm cảnh báo hiện tượng Shuffle nghẽn mạng phân tán?",
            "### Cơ chế `Lazy Evaluation` & Tối ưu `Shuffle` trong Apache Spark\n\n" +
            "- **Transformations (`filter`, `map`, `select`):** Không tính toán ngay mà xây dựng cây kế hoạch logic để bộ tối ưu **Catalyst** gộp và đẩy điều kiện lọc xuống sát tầng đọc tệp (*Predicate Pushdown*).\n" +
            "- **Cảnh báo `Wide Shuffle` (`groupByKey`, `join` bảng lớn):** Phát sinh truyền dữ liệu chéo qua mạng giữa các Worker Nodes. Nếu một bảng nhỏ, hãy dùng **`broadcast(smallDf)`** để triệt tiêu hoàn toàn Shuffle!",
            "Kiểm tra Dữ liệu Lớn: Apache Spark, Catalyst & Apache Kafka",
            QuizDifficulty.Medium,
            [
                ("Vì sao Apache Spark đạt tốc độ xử lý lặp (như huấn luyện Machine Learning) vượt trội so với Hadoop MapReduce cổ điển?", "Vì Spark không dùng mạng LAN", "Vì Spark lưu giữ dữ liệu trung gian phân tán trên bộ nhớ RAM và tối ưu hóa đồ thị thực thi DAG thay vì đọc/ghi đĩa HDFS liên tục", "Vì Spark chỉ chạy trên 1 máy đơn", "Vì Spark không kiểm tra lỗi", "B", "Xử lý trong bộ nhớ (In-Memory Computing) kết hợp Catalyst Optimizer giảm mạnh chi phí I/O đĩa vật lý."),
                ("Trong Apache Spark, thao tác nào dưới đây là một Action kích hoạt việc tính toán thực tế trên cụm máy chủ?", "df.filter(...)", "df.select(...)", "df.count()", "df.withColumn(...)", "C", "filter, select, withColumn là Transformations lười (Lazy); count() đòi hỏi trả kết quả về Driver nên kích hoạt chạy Job."),
                ("Kỹ thuật tối ưu nào giúp Join một bảng Fact cực lớn (1 TB) với một bảng Dimension nhỏ (15 MB) trong Spark mà không gây Shuffle bảng lớn?", "Cartesian Join", "Broadcast Hash Join (phát quảng bá bảng nhỏ tới RAM mọi Executor)", "Sort Merge trên đĩa mềm", "Gộp về 1 Partition duy nhất", "B", "Broadcast gửi bản sao bảng 15 MB tới từng nút Worker để đối chiếu cục bộ mà không cần di chuyển 1 TB dữ liệu qua mạng."),
                ("Giả sử một Kafka Topic có 6 Partitions và một Consumer Group có 8 Consumers cùng đăng ký đọc. Điều gì sẽ xảy ra?", "Cả 8 Consumers cùng đọc trùng lặp mọi bản tin", "6 Consumers nhận mỗi bên 1 Partition để xử lý song song, còn 2 Consumers ở trạng thái chờ dự phòng (Idle)", "Kafka tự xóa bớt 2 Consumers", "Hệ thống báo lỗi dừng chạy", "B", "Mỗi Partition chỉ gắn cho tối đa 1 Consumer trong cùng một Group nhằm bảo toàn thứ tự thông điệp trên Partition đó."),
                ("Định dạng tệp lưu trữ hướng cột (Columnar Storage) nào phổ biến nhất trong Data Lakehouse giúp nén tốt và chỉ đọc đúng các cột cần truy vấn?", "CSV thuần văn bản", "Apache Parquet", "Tệp ảnh BMP", "TXT không cấu trúc", "B", "Parquet lưu dữ liệu theo cột kèm thống kê Min/Max từng khối giúp bỏ qua các khối dữ liệu không khớp điều kiện lọc.")
            ],
            "Bộ thẻ Big Data: Spark DAG & Kafka Streaming",
            [
                ("Lazy Evaluation trong Apache Spark mang lại lợi ích gì?", "Cho phép Catalyst Optimizer gộp nhiều bước biến đổi và đẩy bộ lọc xuống sớm nhất (Predicate Pushdown) trước khi chạy thật.", 2),
                ("Khác biệt giữa Narrow Transformation và Wide Transformation?", "Narrow (map, filter) xử lý ngay trên từng phân vùng cục bộ; Wide (groupBy, join) đòi hỏi truyền dữ liệu Shuffle qua mạng giữa các nút.", 2),
                ("Cơ chế đảm bảo chịu lỗi khi mất 1 nút RAM trong Spark RDD?", "Dựa vào đồ thị phả hệ (Lineage DAG) để tính toán lại đúng phân vùng bị mất từ dữ liệu gốc mà không cần nhân bản tốn RAM.", 1),
                ("Vai trò của Offset trong Apache Kafka?", "Số thứ tự tăng dần định danh duy nhất vị trí bản tin trong từng Partition, giúp Consumer biết chính xác đã đọc tới đâu.", 2),
                ("Data Skew (Lệch phân bố dữ liệu) gây hại gì trên cụm Spark?", "Làm 1 phân vùng phình quá lớn khiến 1 Task chạy chậm kéo lùi toàn bộ thời gian hoàn thành Stage (Straggler Task).", 1)
            ]),

        // 16. OOP201 (Active)
        new(
            "OOP201",
            "Lập trình Hướng đối tượng Chuyên sâu (Java / C#) & Mẫu Thiết kế Design Patterns",
            "Công nghệ phần mềm",
            "Bốn trụ cột OOP, Đa hình động Virtual/Override, Abstract Class vs Interface và ứng dụng thực tiễn Factory, Strategy, Observer, Singleton.",
            RoomStatus.Active, 8,
            [15, 0, 1, 5, 11, 13, 16, 19],
            "vku-oop201-polymorphism-design-patterns.pdf",
            "Thiết kế Hướng đối tượng & GoF Design Patterns Thực chiến trong C# và Java",
            23,
            [
                "## SECTION 1: Bon Tinh chat OOP va Co che Bang Phuong thuc Ao (vtable) cho Da hinh Dong",
                "Da hinh thoi gian chay (Runtime Polymorphism) cho phep goi dung phuong thuc ghi de (override) cua doi tuong thuc te thong qua tham chieu lop cha nho tra cuu Virtual Method Table (vtable).",
                "## SECTION 2: Uu tien Thanh phan hon Ke thua (Composition over Inheritance) va Design Patterns",
                "Mau Strategy cho phep hoan doi thuat toan linh hoat luc chay; mau Observer phat thong bao su kien 1-nhieu; mau Factory Method tach biet logic khoi tao doi tuong khoi ma nghiep vu su dung."
            ],
            "### Tóm tắt Học thuật: Đa hình Hướng đối tượng & GoF Design Patterns\n\n" +
            "| Mẫu Thiết kế | Nhóm (*GoF*) | Bài toán Giải quyết Điển hình |\n" +
            "| :--- | :--- | :--- |\n" +
            "| **Strategy** | *Behavioral* | Hoán đổi thuật toán tính cước / chấm điểm mà không dùng rừng `if-else` |\n" +
            "| **Observer** | *Behavioral* | Thông báo cập nhật trạng thái tự động tới nhiều thành phần đăng ký |\n" +
            "| **Factory Method** | *Creational* | Khởi tạo đối tượng theo ngữ cảnh mà không phụ thuộc lớp cụ thể |",
            "@bot So sánh rõ ràng khi nào chọn Interface, khi nào chọn Abstract Class và cho ví dụ C# về mẫu thiết kế Strategy Pattern?",
            "### Chọn `Interface` hay `Abstract Class` & Ví dụ `Strategy Pattern`\n\n" +
            "- **Dùng `Interface`:** Khi định nghĩa **hợp đồng năng lực** (*Can-Do*) cho phép một lớp thực thi đa giao diện và dễ dàng tiêm phụ thuộc (`DI`).\n" +
            "- **Dùng `Abstract Class`:** Khi các lớp con có quan hệ bản chất (*Is-A*) và cần **chia sẻ sẵn trạng thái / mã nguồn nền tảng**.\n\n" +
            "```csharp\n" +
            "public interface IGradingStrategy\n" +
            "{\n" +
            "    decimal CalculateScore(int correct, int total);\n" +
            "}\n\n" +
            "public sealed class StandardTenScaleStrategy : IGradingStrategy\n" +
            "{\n" +
            "    public decimal CalculateScore(int correct, int total)\n" +
            "        => total == 0 ? 0m : Math.Round((decimal)correct / total * 10m, 2);\n" +
            "}\n" +
            "```",
            "Kiểm tra OOP Chuyên sâu & Mẫu Thiết kế GoF Design Patterns",
            QuizDifficulty.Easy,
            [
                ("Cơ chế Đa hình thời gian chạy (Runtime Polymorphism) trong C# và Java được kích hoạt khi kết hợp cặp từ khóa/cơ chế nào?", "Biến toàn cục tĩnh static", "Khai báo phương thức virtual/abstract ở lớp cơ sở và ghi đè override ở lớp dẫn xuất", "Đặt tên biến viết hoa", "Dùng vòng lặp while", "B", "Trình biên dịch sinh lời gọi gián tiếp qua bảng phương thức ảo (vtable) dựa trên kiểu thực tế của đối tượng trên vùng nhớ Heap."),
                ("Mẫu thiết kế (Design Pattern) nào đóng gói một họ các thuật toán vào các lớp riêng biệt để có thể hoán đổi linh hoạt lúc chạy thay cho chuỗi if-else dài?", "Singleton Pattern", "Strategy Pattern", "Prototype Pattern", "Builder Pattern", "B", "Strategy Pattern tách thuật toán khỏi ngữ cảnh sử dụng, tuân thủ trọn vẹn nguyên lý Open/Closed."),
                ("Một lớp (Class) trong C# hoặc Java có thể kế thừa trực tiếp tối đa bao nhiêu lớp cha và thực thi bao nhiêu Interface?", "Nhiều lớp cha, 1 Interface", "Đơn kế thừa 1 lớp cha trực tiếp, nhưng thực thi không giới hạn số lượng Interfaces", "Không giới hạn lớp cha", "Tối đa 2 lớp cha", "B", "Đơn kế thừa lớp ngăn chặn xung đột trạng thái kim cương (Diamond Problem), trong khi đa Interface mang lại sự linh hoạt tối đa."),
                ("Nguyên tắc thiết kế 'Favor Composition over Inheritance' khuyên lập trình viên điều gì?", "Xóa hết mọi Interface", "Ưu tiên lắp ghép các đối tượng thành phần qua thuộc tính/Interface thay vì lạm dụng cây kế thừa sâu cứng nhắc", "Viết toàn bộ chương trình trong hàm Main", "Không dùng Constructor", "B", "Cây kế thừa quá sâu làm lớp con phụ thuộc chặt vào cài đặt nội bộ lớp cha; Composition linh hoạt và dễ kiểm thử hơn nhiều."),
                ("Tính Đóng gói (Encapsulation) trong lập trình hướng đối tượng được thể hiện chuẩn xác nhất qua cách làm nào?", "Để tất cả các trường dữ liệu là public", "Che giấu dữ liệu nội bộ bằng phạm vi private và kiểm soát truy cập/ràng buộc hợp lệ thông qua các phương thức hoặc Properties", "Chỉ dùng kiểu chuỗi", "Không tách lớp", "B", "Encapsulation bảo vệ bất biến nghiệp vụ (Domain Invariants) của đối tượng khỏi sự thay đổi tùy tiện từ bên ngoài.")
            ],
            "Bộ thẻ OOP & GoF Design Patterns Thực chiến",
            [
                ("Khác biệt giữa Method Overloading (Nạp chồng) và Method Overriding (Ghi đè)?", "Overloading: cùng tên hàm khác danh sách tham số (quyết định lúc biên dịch); Overriding: lớp con định nghĩa lại hàm ảo của lớp cha (quyết định lúc chạy).", 2),
                ("Ba nhóm chính của 23 mẫu thiết kế Gang of Four (GoF)?", "1. Creational (Khởi tạo), 2. Structural (Cấu trúc), 3. Behavioral (Hành vi).", 2),
                ("Rủi ro khi lạm dụng Singleton có trạng thái toàn cục khả biến?", "Gây phụ thuộc ngầm chặt chẽ, khó chạy Unit Test song song và dễ phát sinh lỗi đa luồng nếu không đồng bộ hóa.", 1),
                ("Mẫu thiết kế Adapter dùng để làm gì?", "Chuyển đổi giao diện của một lớp có sẵn sang giao diện khác mà phía Client đang mong đợi để hai bên làm việc cùng nhau.", 2),
                ("Từ khóa sealed (C#) hoặc final (Java) đặt trước tên lớp có ý nghĩa gì?", "Ngăn không cho bất kỳ lớp nào khác kế thừa từ lớp đó, bảo toàn tính bất biến thiết kế và giúp JIT tối ưu lời gọi hàm.", 2)
            ]),

        // 17. ARC205 (Active)
        new(
            "ARC205",
            "Kiến trúc Máy tính, Pipeline Vi xử lý RISC-V & Tối ưu Bộ nhớ Đệm Cache",
            "Hệ điều hành",
            "Chu trình 5 tầng Pipeline (IF-ID-EX-MEM-WB), xung đột dữ liệu/nhánh, nguyên lý cục bộ tham chiếu và ánh xạ Cache L1/L2/L3.",
            RoomStatus.Active, 7,
            [16, 2, 7, 10, 13, 15, 18],
            "vku-arc205-cpu-pipeline-cache-hierarchy.pdf",
            "Kiến trúc Máy tính Hiện đại: Kỹ thuật Ống dẫn lệnh 5 Tầng Pipeline & Phân cấp Bộ nhớ Cache",
            19,
            [
                "## SECTION 1: Ky thuat Ong dan lenh 5 Tang (IF - ID - EX - MEM - WB) va Xu ly Hazard",
                "Pipeline chia qua trinh thuc thi lenh thanh 5 cong doan song song giup tang thong luong lenh xap xi 5 lan. Ba loai xung dot gom Structural Hazard, Data Hazard (giai quyet bang Forwarding / Bypassing) va Control Hazard (giai quyet bang Branch Prediction).",
                "## SECTION 2: Phan cap Bo nho Cache SRAM va Thoi gian Truy cap Bo nho Trung binh (AMAT)",
                "Dua tren Nguyen ly Cuc bo Thoi gian (Temporal Locality) va Cuc bo Khong gian (Spatial Locality), CPU tich hop Cache L1/L2/L3 sieu nhanh. Cong thuc tinh thoi gian truy cap trung binh AMAT = HitTime + MissRate * MissPenalty."
            ],
            "### Tóm tắt Học thuật: CPU Pipeline & Hiệu năng Bộ nhớ Cache\n" +
            "- **Thời gian thực thi chương trình CPU:**\n" +
            "$$T_{\\text{CPU}} = \\text{IC} \\times \\text{CPI} \\times T_{\\text{clock}} = \\frac{\\text{IC} \\times \\text{CPI}}{f_{\\text{clock}}}$$\n" +
            "- **Thời gian truy cập bộ nhớ trung binh (*AMAT*):**\n" +
            "$$\\text{AMAT} = T_{\\text{hit}} + \\text{MR} \\times T_{\\text{miss penalty}}$$",
            "@bot Giải thích vì sao duyệt mảng 2 chiều theo từng dòng (Row-major) trong C/C# lại nhanh gấp nhiều lần duyệt theo từng cột nhờ bộ nhớ Cache?",
            "### Tác động của `Spatial Locality` & `Cache Line` khi Duyệt Ma trận\n\n" +
            "- Trong C/C++/C#, mảng 2 chiều lưu trữ liên tiếp theo từng hàng (*Row-major*). Mỗi lần nạp từ RAM lên L1 Cache, CPU lấy nguyên một khối **`Cache Line = 64 bytes`** (chứa liền 16 số nguyên `int` 4-byte).\n" +
            "- **Duyệt theo hàng (`a[i][j]`):** 15/16 lần đọc tiếp theo trúng ngay `L1 Cache Hit` ($\\approx 1\\text{ ns}$).\n" +
            "- **Duyệt theo cột (`a[j][i]`):** Nhảy cóc qua cả hàng dài gây `Cache Miss` liên tục phải chờ RAM ($\\approx 80\\text{ ns}$), chậm hơn từ **5 đến 12 lần**!",
            "Trắc nghiệm Kiến trúc Máy tính: Pipeline 5 Tầng & Bộ nhớ Cache",
            QuizDifficulty.Medium,
            [
                ("Cho bộ nhớ Cache L1 có thời gian trúng Hit Time = 1 ns, tỷ lệ trượt Miss Rate = 5% và chi phí phạt Miss Penalty = 100 ns. Giá trị AMAT bằng bao nhiêu?", "1.5 ns", "6.0 ns", "51.0 ns", "101.0 ns", "B", "Áp dụng công thức AMAT = HitTime + MissRate * MissPenalty = 1 + 0.05 * 100 = 1 + 5 = 6.0 ns."),
                ("Kỹ thuật phần cứng nào trong vi xử lý Pipeline chuyển trực tiếp kết quả vừa tính xong ở đầu ra tầng EX về ngay đầu vào tầng EX của lệnh kế tiếp mà không chờ ghi vào thanh ghi?", "Virtual Paging", "Data Forwarding (Bypassing)", "Disk Defragmentation", "BIOS Interrupt", "B", "Forwarding nối tắt đường dữ liệu nội bộ giúp triệt tiêu hầu hết chu kỳ dừng (Stall bubbles) do phụ thuộc dữ liệu."),
                ("Năm giai đoạn chuẩn theo thứ tự của đường ống lệnh RISC cổ điển gồm?", "IF -> ID -> EX -> MEM -> WB", "WB -> MEM -> EX -> ID -> IF", "COMPILE -> LINK -> LOAD -> RUN -> STOP", "L1 -> L2 -> L3 -> RAM -> SSD", "A", "Instruction Fetch -> Instruction Decode -> Execute -> Memory Access -> Write Back."),
                ("Việc chương trình vừa đọc phần tử mảng A[i] thì xác suất rất cao ngay sau đó sẽ đọc phần tử liền kề A[i+1] phản ánh nguyên lý nào?", "Nguyên lý Cục bộ Không gian (Spatial Locality)", "Nguyên lý Cục bộ Thời gian (Temporal Locality)", "Nguyên lý Bất định", "Định luật Amdahl", "A", "Spatial Locality là lý do phần cứng luôn nạp cả một khối Cache Line (64 bytes) lân cận lên bộ nhớ đệm."),
                ("Theo Định luật Amdahl, nếu tối ưu một thành phần chiếm 50% thời gian chạy nhanh lên gấp vô hạn lần thì tốc độ tổng thể toàn chương trình tăng tối đa bao nhiêu lần?", "Vô hạn lần", "Tối đa 2 lần", "50 lần", "10 lần", "B", "Speedup_max = 1 / (1 - p) = 1 / (1 - 0.5) = 2 lần; phần tuần tự còn lại giới hạn mức tăng tốc tối đa.")
            ],
            "Bộ thẻ Kiến trúc Máy tính: Pipeline & Cache",
            [
                ("Công thức hiệu năng cơ bản của CPU?", "CPU Time = Instruction Count (IC) * Cycles Per Instruction (CPI) * Clock Cycle Time.", 2),
                ("Công thức tính AMAT (Average Memory Access Time)?", "AMAT = Hit Time + (Miss Rate * Miss Penalty).", 2),
                ("Ba loại xung đột (Hazards) trong CPU Pipeline?", "1. Structural Hazard (tranh chấp phần cứng), 2. Data Hazard (phụ thuộc dữ liệu), 3. Control Hazard (lệnh rẽ nhánh).", 2),
                ("Khác biệt giữa chính sách ghi Cache Write-Through và Write-Back?", "Write-Through ghi đồng thời vào cả Cache và RAM; Write-Back chỉ ghi vào Cache (đánh dấu Dirty bit) và chỉ đẩy xuống RAM khi khối đó bị thay thế.", 1),
                ("Phát biểu công thức Định luật Amdahl khi tăng tốc phần song song p lên S lần?", "Speedup = 1 / [(1 - p) + (p / S)].", 1)
            ]),

        // 18. UIX304 (Active)
        new(
            "UIX304",
            "Thiết kế Giao diện & Trải nghiệm Người dùng (UI/UX) Chuẩn Design System",
            "Phát triển Web",
            "Hệ thống Design Tokens, lưới 8pt Grid, tỷ lệ tương phản WCAG AA/AAA, Định luật Fitts, Định luật Hick và thiết kế Vi tương tác.",
            RoomStatus.Active, 6,
            [17, 1, 4, 9, 11, 14, 18],
            "vku-uix304-design-system-accessibility.pdf",
            "Giáo trình Thiết kế Sản phẩm Số UI/UX: Hệ thống Lưới 8pt, Định luật Tâm lý học & Chuẩn WCAG",
            18,
            [
                "## SECTION 1: Cac Dinh luat Tam ly hoc Nen tang trong UX (Fitts, Hick, Jakob & Doherty Threshold)",
                "Dinh luat Fitts chi ra thoi gian di chuyen con tro toi muc tieu ti le thuan voi logarit khoang cach va ti le nghich voi kich thuoc nut bam. Nguong Doherty (400ms) khang dinh he thong phan hoi duoi 400ms giu nguoi dung trong trang thai tap trung cao do.",
                "## SECTION 2: He thong Design Tokens, Luoi 8pt va Tieu chuan Tuong phan WCAG 2.1",
                "Design System chuan hoa mau sac ngữ nghĩa (Primary, Surface, Border, Semantic Status), thang chu (Typography Scale) va khoang cach boi so cua 4px/8px. Ty le tuong phan van ban thuong tren nen phai dat toi thieu 4.5:1 (chuan WCAG AA)."
            ],
            "### Tóm tắt Học thuật: Định luật UX & Chuẩn Tương phản WCAG\n" +
            "- **Định luật Fitts (*Fitts's Law*):** Thời gian trỏ trúng nút mục tiêu có độ rộng $W$ ở khoảng cách $D$:\n" +
            "$$T = a + b \\log_2\\!\\left(1 + \\frac{D}{W}\\right)$$\n" +
            "- **Định luật Hick (*Hick's Law*):** Thời gian ra quyết định tăng theo logarit số lựa chọn $n$: $RT = a + b \\log_2(n + 1)$.\n" +
            "- **Chuẩn tương phản màu sắc `WCAG AA`:** Tối thiểu **`4.5 : 1`** cho chữ văn bản chuẩn và **`3.0 : 1`** cho chữ tiêu đề lớn.",
            "@bot Tóm tắt ý nghĩa ứng dụng thực tế của Định luật Fitts, Định luật Hick và hiệu ứng Skeleton Shimmer trong thiết kế giao diện web hiện đại?",
            "### Ba Nguyên lý Vàng Nâng tầm Trải nghiệm Người dùng (`UX`)\n\n" +
            "1. **Định luật Fitts:** Các nút hành động chính (*Gửi tin nhắn, Nộp bài, Đăng nhập*) cần đủ cao (`40px - 48px`) và đặt gần vùng thao tác tự nhiên của người dùng.\n" +
            "2. **Định luật Hick:** Không bày quá nhiều nút rối mắt cùng lúc; gom các thao tác nâng cao vào thanh tìm kiếm nhanh **`Ctrl + K` (*Progressive Disclosure*)**.\n" +
            "3. **Skeleton Shimmer thay vòng xoay chờ:** Tạo cảm giác tốc độ tải trang nhanh hơn **30%** nhờ định hình trước bố cục thị giác.",
            "Kiểm tra Thiết kế UI/UX, Tâm lý học Tương tác & Chuẩn WCAG",
            QuizDifficulty.Easy,
            [
                ("Theo tiêu chuẩn khả năng tiếp cận web WCAG 2.1 mức AA, tỷ lệ tương phản tối thiểu giữa chữ văn bản thường và màu nền là bao nhiêu?", "1.5 : 1", "4.5 : 1", "21 : 1", "1 : 1", "B", "Tỷ lệ 4.5:1 đảm bảo người dùng đọc rõ nội dung trên nhiều điều kiện ánh sáng màn hình khác nhau."),
                ("Định luật UX nào khẳng định rằng khi số lượng lựa chọn trên màn hình càng nhiều và phức tạp thì thời gian người dùng đưa ra quyết định càng lâu?", "Định luật Moore", "Định luật Hick (Hick's Law)", "Định luật Ohm", "Định luật Newton", "B", "Hick's Law khuyến khích tối giản thanh điều hướng và phân nhóm thông tin mạch lạc."),
                ("Theo Định luật Fitts, làm cách nào để giảm thời gian người dùng bấm trúng một nút hành động quan trọng trên màn hình cảm ứng?", "Làm nút nhỏ đi và đặt xa ngón tay", "Tăng diện tích vùng chạm ( tối thiểu 44x44px ) và đặt gần vùng thao tác hiện tại", "Đổi sang phông chữ nghiêng", "Ẩn nút vào menu 3 cấp", "B", "Tăng bề rộng mục tiêu W và giảm khoảng cách D làm giảm trực tiếp hàm log2(1 + D/W)."),
                ("Hệ thống quy ước khoảng cách (Spacing System) phổ biến nhất trong các Design System hiện đại sử dụng bội số cơ sở nào?", "Bội số số nguyên tố 7px, 11px, 13px", "Hệ lưới 4px / 8pt Grid (4, 8, 12, 16, 24, 32, 48px)", "Số thập phân ngẫu nhiên", "Đơn vị centimet giấy", "B", "8pt Grid chia chẵn trên hầu hết mật độ điểm ảnh màn hình (1x, 1.5x, 2x, 3x) giúp giao diện sắc nét và nhịp nhàng."),
                ("Vì sao thay thế dòng chữ 'Đang tải...' đơn điệu bằng khung xương chuyển động (Skeleton Shimmer) lại cải thiện trải nghiệm UX?", "Vì không cần kết nối mạng", "Vì định hình trước bố cục trang, giảm cảm giác chờ đợi chủ quan (Perceived Latency) và tránh giật nhảy bố cục (CLS)", "Vì giảm kích thước cơ sở dữ liệu", "Vì tự động dịch ngôn ngữ", "B", "Skeleton giữ đúng chiều cao dự kiến của phần tử sắp xuất hiện giúp mắt người dùng không bị hẫng khi dữ liệu đổ về.")
            ],
            "Bộ thẻ UI/UX Design System & Laws of UX",
            [
                ("Công thức và ý nghĩa thực tế của Định luật Fitts?", "T = a + b * log2(1 + D/W): mục tiêu càng lớn và càng gần con trỏ thì càng dễ bấm nhanh và ít bấm nhầm.", 2),
                ("Ngưỡng Doherty (Doherty Threshold - 400ms) nói lên điều gì?", "Khi máy tính phản hồi thao tác dưới 400ms, năng suất người dùng tăng vọt vì chuỗi suy nghĩ không bị đứt quãng.", 2),
                ("Định luật Jakob (Jakob's Law) trong thiết kế sản phẩm?", "Người dùng dành phần lớn thời gian trên các sản phẩm quen thuộc khác, vì vậy họ kỳ vọng phím tắt (như Ctrl+K, Esc) hoạt động theo chuẩn quen thuộc.", 2),
                ("Design Tokens là gì?", "Các biến nguyên tử lưu giá trị màu sắc, bo góc, bóng đổ, chữ viết giúp đồng bộ tức thì giữa chế độ Light Mode và Dark Mode.", 1),
                ("Ba trạng thái giao diện bắt buộc phải thiết kế cho mọi danh sách dữ liệu?", "1. Loading State (Skeleton), 2. Empty State (có nút gợi ý hành động), 3. Populated / Error State.", 2)
            ]),

        // 19. TST307 (Active)
        new(
            "TST307",
            "Kiểm thử Phần mềm Tự động (Automation Testing), TDD & Kiểm thử Hiệu năng",
            "Kiểm thử phần mềm",
            "Kỹ thuật Phân vùng tương đương, Phân tích giá trị biên, quy trình Red-Green-Refactor TDD, viết Unit Test xUnit và kiểm thử chịu tải.",
            RoomStatus.Active, 5,
            [18, 3, 6, 9, 12, 16, 19],
            "vku-tst307-automation-testing-tdd.pdf",
            "Đảm bảo Chất lượng & Kiểm thử Phần mềm: Thiết kế Test Case, Chu trình TDD & xUnit",
            20,
            [
                "## SECTION 1: Ky thuat Thiet ke Test Case Hop den (Equivalence Partitioning & Boundary Value Analysis)",
                "Phan vung tuong duong chia mien dau vao thanh cac lop hop le va khong hop le de giam so luong ca kiem thu du thua. Phan tich gia tri bien (BVA) tap trung kiem tra ngay tai bien min, min-1, min+1, max-1, max, max+1 vi 70% loi nghiep vu phat sinh tai ranh gioi dieu kien.",
                "## SECTION 2: Mau AAA (Arrange - Act - Assert) va Phat trien Huong Kiem thu TDD",
                "Moi Unit Test chuan muc gom 3 khoi tach biet: Arrange (chuan bi du lieu & Test Doubles), Act (goi phuong thuc can kiem tra) va Assert (doi chieu ket qua). Quy trinh TDD tuan theo vong lap ngan Red (viet test that bai) -> Green (viet code toi thieu de pass) -> Refactor (lam sach ma nguon)."
            ],
            "### Tóm tắt Học thuật: Kỹ thuật Kiểm thử Hộp đen & Quy trình TDD\n\n" +
            "| Kỹ thuật Kiểm thử | Trọng tâm Phát hiện Lỗi | Ví dụ với miền điểm `[0..10]` |\n" +
            "| :--- | :--- | :--- |\n" +
            "| **Phân vùng tương đương** | Đại diện mỗi miền hợp lệ / sai | `-5` (Sai), `7` (Đúng), `14` (Sai) |\n" +
            "| **Phân tích giá trị biên** | Lỗi lệch dấu `<` và `<=` tại biên | `-0.01`, `0`, `0.01`, `9.99`, `10`, `10.01` |\n\n" +
            "- **Chu trình TDD:** `Red` (Viết test đỏ) $\\longrightarrow$ `Green` (Code vượt bài test) $\\longrightarrow$ `Refactor` (Tối ưu cấu trúc).",
            "@bot Giải thích mẫu viết kiểm thử đơn vị Arrange - Act - Assert (AAA) trong xUnit C# kèm ví dụ kiểm tra thuật toán chấm điểm trắc nghiệm?",
            "### Cấu trúc Chuẩn `Arrange - Act - Assert` trong `xUnit`\n\n" +
            "```csharp\n" +
            "[Fact]\n" +
            "public void CalculateQuizScore_When8Of10Correct_ShouldReturn8Point00()\n" +
            "{\n" +
            "    // 1. Arrange: Khởi tạo dữ liệu đầu vào chuẩn xác\n" +
            "    int correctAnswers = 8;\n" +
            "    int totalQuestions = 10;\n\n" +
            "    // 2. Act: Thực thi hàm nghiệp vụ cần kiểm chứng\n" +
            "    decimal score = Math.Round((decimal)correctAnswers / totalQuestions * 10m, 2);\n\n" +
            "    // 3. Assert: Khẳng định kết quả đầu ra khớp kỳ vọng\n" +
            "    Assert.Equal(8.00m, score);\n" +
            "}\n" +
            "```",
            "Kiểm tra Kỹ thuật Kiểm thử Phần mềm, Giá trị Biên & TDD",
            QuizDifficulty.Easy,
            [
                ("Giả sử ô nhập Số lượng câu hỏi Quiz chỉ chấp nhận số nguyên từ 3 đến 20. Bộ giá trị nào dưới đây là bộ kiểm thử Giá trị biên (Boundary Value Analysis) chuẩn nhất?", "5, 10, 15", "2, 3, 4 và 19, 20, 21", "-100, 0, 1000", "Chỉ thử duy nhất số 10", "B", "Phân tích giá trị biên kiểm tra sát ngay ngoài biên và trên biên: (min-1, min, min+1) = (2, 3, 4) và (max-1, max, max+1) = (19, 20, 21)."),
                ("Ba bước tuần hoàn liên tiếp trong phương pháp Phát triển Hướng Kiểm thử (TDD) là gì?", "Code -> Deploy -> Fix", "Red (Viết test thất bại) -> Green (Viết code vừa đủ để pass) -> Refactor (Làm sạch code)", "Design -> Print -> Archive", "Compile -> Zip -> Upload", "B", "Viết bài kiểm thử trước giúp định hình rõ yêu cầu giao diện hàm và đảm bảo độ bao phủ kiểm thử tự nhiên từ đầu."),
                ("Trong mẫu viết Unit Test AAA, phần 'Act' thực hiện nhiệm vụ gì?", "Xóa cơ sở dữ liệu", "Gọi duy nhất hành vi / phương thức mục tiêu đang cần kiểm thử để thu kết quả", "Cài đặt thư viện NuGet", "Viết tài liệu hướng dẫn", "B", "Tách bạch Arrange, Act và Assert giúp người đọc nhìn thấy ngay hàm nào đang được kiểm tra và kỳ vọng đầu ra là gì."),
                ("Đặc tính bắt buộc của một bộ Unit Tests tốt theo nguyên tắc F.I.R.S.T là gì?", "Phải phụ thuộc thứ tự chạy và cần mạng Internet", "Fast (Chạy cực nhanh), Independent (Độc lập không ảnh hưởng nhau), Repeatable (Lặp lại nhất quán), Self-Validating, Timely", "Mỗi test phải chạy ít nhất 5 phút", "Phải kiểm tra bằng mắt thường", "B", "Các Unit Tests độc lập sử dụng InMemory DB riêng biệt có thể chạy song song hàng trăm bài trong vài giây."),
                ("Chỉ số P95 Latency = 180ms trong báo cáo kiểm thử chịu tải API có ý nghĩa chính xác là gì?", "95% máy chủ bị lỗi", "95% tổng số yêu cầu được phản hồi trong thời gian từ 180 mili-giây trở xuống (chỉ 5% chậm hơn 180ms)", "Trung bình cộng mọi yêu cầu là 95ms", "Chỉ có 95 người dùng", "B", "Phân vị P95/P99 phản ánh trải nghiệm thực tế của người dùng chính xác hơn nhiều so với số trung bình cộng dễ bị sai lệch.")
            ],
            "Bộ thẻ Automation Testing, BVA & TDD",
            [
                ("Khác biệt giữa Kiểm thử Hộp đen (Black-box) và Hộp trắng (White-box)?", "Hộp đen dựa trên đặc tả đầu vào/đầu ra không cần xem mã nguồn; Hộp trắng dựa trên cấu trúc nhánh/đường đi bên trong mã nguồn.", 2),
                ("Ý nghĩa của 5 chữ cái F.I.R.S.T trong Unit Testing?", "Fast, Independent/Isolated, Repeatable, Self-Validating (tự kết luận Pass/Fail), Timely.", 2),
                ("Regression Testing (Kiểm thử hồi quy) dùng để làm gì?", "Chạy lại bộ kiểm thử sau khi sửa lỗi hoặc thêm tính năng mới để chắc chắn không làm hỏng các chức năng cũ đang chạy tốt.", 2),
                ("Sự khác nhau giữa Stub và Mock trong kiểm thử?", "Stub cung cấp dữ liệu trả về định sẵn cho hàm phụ thuộc; Mock cho phép kiểm chứng thêm hành vi tương tác (ví dụ hàm gửi mail có được gọi đúng 1 lần không).", 1),
                ("Tại sao không nên chỉ nhìn chỉ số Average Response Time khi kiểm thử tải?", "Vì số trung bình che khuất các yêu cầu bị treo rất lâu ở đuôi phân phối; cần đo P95 và P99 Latency.", 1)
            ]),

        // 20. DIS406 (Active)
        new(
            "DIS406",
            "Hệ thống Phân tán, Định lý CAP, Đồng thuận Raft & Hàng đợi Thông điệp",
            "Điện toán đám mây",
            "Định lý CAP, tính nhất quán cuối cùng (Eventual Consistency), bầu chọn Leader trong Raft, khóa phân tán Redis và chống xử lý trùng lặp.",
            RoomStatus.Active, 4,
            [19, 2, 5, 8, 13, 15, 17],
            "vku-dis406-distributed-cap-raft.pdf",
            "Nguyên lý Hệ thống Phân tán: Định lý CAP, Đồng thuận Raft & Thiết kế Chịu lỗi Quy mô Lớn",
            22,
            [
                "## SECTION 1: Dinh ly CAP (Consistency - Availability - Partition Tolerance) trong He Phan tan",
                "Trong mang phan tan thuc te, Phan manh mang (Network Partition - P) la khong the tranh khoi. Khi xay ra dut ket noi giua cac nut, kien truc su phai chon danh doi giua CP (uu tien Nhat quan tuyet doi, tu choi tam thoi ghi nhu etcd/ZooKeeper) hoac AP (uu tien San sang phan hoi, chap nhan Eventual Consistency nhu Cassandra/Dynamo).",
                "## SECTION 2: Thuat toan Dong thuan Raft va Quy tac Da so Qua ban (Quorum Majority)",
                "Mot cum Raft gom N nut (thuong la so le 3 hoac 5) duy tri 1 Leader, cac Followers va Candidates. Mot ban ghi Log chi duoc Commit khi da nhan ban thanh cong tren da so qua ban Q = floor(N / 2) + 1 nut, chiu duoc toi da floor((N - 1) / 2) nut sap."
            ],
            "### Tóm tắt Học thuật: Định lý CAP & Đa số Quá bán Quorum\n" +
            "- **Quy tắc Quorum trong cụm đồng thuận `Raft` ($N$ nút):**\n" +
            "$$Q = \\left\\lfloor \\frac{N}{2} \\right\\rfloor + 1, \\qquad F_{\\max} = \\left\\lfloor \\frac{N - 1}{2} \\right\\rfloor$$\n" +
            "  *(Ví dụ cụm $N = 5$ nút cần $Q = 3$ nút xác nhận và chịu được tối đa $F_{\\max} = 2$ nút ngừng hoạt động).* \n" +
            "- **Định lý CAP khi đứt mạng (`P`):** Phải chọn đánh đổi giữa **`CP`** (*Nhất quán chặt*) hoặc **`AP`** (*Sẵn sàng cao*).",
            "@bot Giải thích vì sao các cụm đồng thuận phân tán như Raft / etcd luôn triển khai số lượng nút lẻ (3 hoặc 5 nút) thay vì số chẵn (4 nút)?",
            "### Vì Sao Cụm Đồng thuận `Raft` Luôn Chọn Số Nút Lẻ ($3, 5, 7$)?\n\n" +
            "Áp dụng công thức đa số quá bán $Q = \\lfloor N/2 \\rfloor + 1$:\n\n" +
            "| Quy mô Cụm ($N$) | Số nút cần xác nhận ($Q$) | Số nút chịu lỗi tối đa ($N - Q$) |\n" +
            "| :---: | :---: | :---: |\n" +
            "| **3 nút** | $2$ nút | Chịu sập **1 nút** |\n" +
            "| **4 nút** | $3$ nút | Cũng chỉ chịu sập **1 nút** |\n" +
            "| **5 nút** | $3$ nút | Chịu sập **2 nút** |\n\n" +
            "- Cụm **4 nút** tốn thêm chi phí phần cứng và cần nhiều xác nhận hơn cụm **3 nút** nhưng khả năng chịu lỗi vẫn chỉ là **1 nút** và còn dễ bị chia đôi mạng (*Split-Brain 2-2*)!",
            "Đánh giá Hệ thống Phân tán: Định lý CAP, Raft & Quorum",
            QuizDifficulty.Hard,
            [
                ("Một cụm cơ sở dữ liệu phân tán chạy thuật toán đồng thuận Raft gồm N = 5 máy chủ có thể tiếp tục hoạt động bình thường khi tối đa bao nhiêu máy chủ bị sập cùng lúc?", "1 máy chủ", "2 máy chủ", "4 máy chủ", "5 máy chủ", "B", "Quorum của 5 nút là floor(5/2) + 1 = 3 nút còn sống, nên hệ thống chịu lỗi được tối đa 5 - 3 = 2 nút sập."),
                ("Theo Định lý CAP, khi xảy ra sự cố phân mảnh đường truyền mạng (Network Partition) giữa hai trung tâm dữ liệu, hệ thống bắt buộc phải đánh đổi giữa hai tính chất nào?", "Tốc độ CPU và Dung lượng RAM", "Tính Nhất quán tức thời (Consistency) và Tính Sẵn sàng phục vụ (Availability)", "Bảo mật và Giao diện", "SQL và HTML", "B", "Khi các nút không liên lạc được với nhau, hoặc phải chặn ghi để giữ dữ liệu đồng nhất (CP), hoặc cho phép ghi cục bộ rồi đồng bộ sau (AP)."),
                ("Kỹ thuật đính kèm khóa định danh duy nhất (Idempotency-Key) vào yêu cầu thanh toán trong hệ phân tán giúp giải quyết rủi ro nào?", "Tăng dung lượng ảnh", "Ngăn chặn việc trừ tiền hai lần khi Client tự động gửi lại (Retry) do gói phản hồi mạng bị trễ/rớt", "Thay thế hoàn toàn mật khẩu", "Tắt tường lửa", "B", "Máy chủ lưu kết quả theo Idempotency-Key; nếu nhận lại cùng khóa đó sẽ trả ngay kết quả cũ mà không thực thi trùng lặp."),
                ("Hiện tượng rất nhiều khóa trong bộ nhớ đệm Redis cùng hết hạn TTL tại đúng một giây khiến hàng vạn truy vấn ập thẳng xuống SQL Server gọi là gì?", "Cache Hit", "Cache Avalanche / Cache Stampede (Sập tuyết bộ nhớ đệm)", "Memory Leak", "SQL Injection", "B", "Giải pháp chuẩn là cộng thêm một khoảng nhiễu ngẫu nhiên nhỏ (Jitter) vào thời gian hết hạn TTL và dùng khóa đơn (Singleflight)."),
                ("Mẫu thiết kế giao dịch phân tán nào thay thế khóa 2 pha (2PC) bằng chuỗi các giao dịch cục bộ kèm giao dịch bù trừ (Compensating Transactions) khi có bước thất bại?", "Singleton Pattern", "Saga Pattern", "Adapter Pattern", "Decorator Pattern", "B", "Saga không giữ khóa liên dịch vụ kéo dài, đảm bảo tính nhất quán cuối cùng thông qua các bước hoàn tác nghiệp vụ tường minh.")
            ],
            "Bộ thẻ Distributed Systems: CAP, Raft & Caching",
            [
                ("Ba chữ cái C - A - P trong Định lý CAP của Brewer?", "Consistency (Nhất quán), Availability (Sẵn sàng), Partition Tolerance (Chịu phân mảnh mạng).", 2),
                ("Công thức tính Quorum tối thiểu để tránh Split-Brain?", "Q = floor(N / 2) + 1 (đa số quá bán). Với đọc/ghi Quorum: R + W > N đảm bảo luôn đọc trúng bản ghi mới nhất.", 2),
                ("Ba vai trò trạng thái của một nút trong thuật toán Raft?", "Leader (Xử lý ghi & nhân bản log), Follower (Nhận log & heartbeat), Candidate (Ứng cử khi hết Election Timeout).", 2),
                ("Chiến lược đọc/ghi Cache-Aside (Lazy Loading) hoạt động thế nào?", "Ứng dụng kiểm tra Redis trước; nếu Cache Miss mới đọc từ DB rồi nạp ngược lên Redis kèm TTL; khi cập nhật DB thì xóa khóa Cache tương ứng.", 2),
                ("Circuit Breaker (Cầu dao ngắt mạch) trong Microservices dùng để làm gì?", "Tự động ngắt nhanh lời gọi tới một dịch vụ đích đang chết liên tục để ngăn lỗi lan truyền làm treo toàn bộ hệ thống.", 1)
            ]),

        // 21. PRJ410 (Active - Phòng thứ 20 mở công khai!)
        new(
            "PRJ410",
            "Đồ án Chuyên ngành CNTT: Kỹ năng Bảo vệ Hội đồng & Viết Báo cáo Khoa học",
            "Đồ án Chuyên ngành",
            "Cấu trúc báo cáo chuẩn IEEE/VKU, vẽ biểu đồ Use Case, Sequence, ERD 3NF, kịch bản Demo không lỗi và bộ câu hỏi phản biện thường gặp.",
            RoomStatus.Active, 2,
            [0, 1, 2, 4, 7, 11, 14, 17, 19],
            "vku-prj410-capstone-defense-handbook.pdf",
            "Cẩm nang Đồ án Chuyên ngành VKU: Chuẩn hóa Sơ đồ Kiến trúc, Kịch bản Demo & Phản biện Hội đồng",
            17,
            [
                "## SECTION 1: Cau truc Bao cao Do an Chuan muc va Nguyen tac Trinh bay Slide 10-20-30",
                "Bao cao gom 4 chuong mach lac: (1) Tong quan de tai & khao sat hien trang; (2) Phan tich thiet ke he thong (Use Case, Sequence Diagram, ERD 3NF, Kien truc phan tang); (3) Trien khai cong nghe & Thuat toan trong tam; (4) Kiem thu thuc nghiem & Huong phat trien.",
                "## SECTION 2: Kinh nghiem Vang khi Demo Truc tiep va Tra loi Phan bien Hoi dong",
                "Luon chuan bi bo du lieu mau chan thuc phong phu, kiem tra ky luong End-to-End tu giao dien xuong CSDL, giai thich ro luong xu ly tai cac diem kho nhu JWT, WebSockets SignalR, chong XSS/SQLi va tich hop AI Structured Output."
            ],
            "### Tóm tắt Học thuật: Cẩm nang Bảo vệ Đồ án Chuyên ngành Xuất sắc\n\n" +
            "| Phần Đánh giá | Tỷ trọng Điểm | Tiêu chí Ghi điểm Tuyệt đối trước Hội đồng |\n" +
            "| :--- | :---: | :--- |\n" +
            "| **Kiến trúc & CSDL** | `30%` | Phân tầng rõ ràng (*Clean Architecture*), CSDL chuẩn `3NF` |\n" +
            "| **Demo Sản phẩm Sống** | `40%` | Luồng chạy thật 100%, dữ liệu phong phú, giao diện mượt |\n" +
            "| **Trả lời Phản biện** | `30%` | Nắm chắc mã nguồn, giải thích rõ thuật toán & bảo mật |",
            "@bot Tổng hợp 5 câu hỏi phản biện đắt giá nhất mà Hội đồng thường hỏi với đề tài Web thời gian thực tích hợp AI và gợi ý hướng trả lời ngắn gọn?",
            "### 5 Câu hỏi Phản biện Trọng tâm & Hướng Trả lời Ghi Điểm A+\n\n" +
            "1. **Làm sao ngăn AI bịa đặt thông tin ngoài bài giảng?** $\\rightarrow$ Áp dụng kỹ thuật **Grounding ngữ cảnh PDF** + cửa sổ trượt tin nhắn gần nhất và ép đầu ra **JSON Schema**.\n" +
            "2. **Vì sao SignalR truyền JWT qua Query String?** $\\rightarrow$ Do WebSocket API trên trình duyệt không hỗ trợ gắn tùy chỉnh Header `Authorization` lúc bắt tay.\n" +
            "3. **Làm sao tránh lỗi `ObjectDisposedException` khi gọi AI bất đồng bộ?** $\\rightarrow$ Dùng `IServiceScopeFactory` khởi tạo Scope độc lập mới và phát `BotThinking(false)` trong `finally`.\n" +
            "4. **Cơ sở dữ liệu xử lý `Multiple Cascade Paths` trên SQL Server thế nào?** $\\rightarrow$ Thiết lập `DeleteBehavior.Restrict` cho các nhánh khóa ngoại phụ hội tụ.\n" +
            "5. **Ứng dụng bảo vệ chống `XSS` ra sao khi vừa có chat người dùng vừa có Markdown AI?** $\\rightarrow$ Mọi tin nhắn sinh viên đi qua `escapeHtml()`, còn khối Markdown AI lọc sạch thẻ `<script>` và thuộc tính `on*`.",
            "Trắc nghiệm Tổng duyệt Kiến thức Bảo vệ Đồ án Chuyên ngành CNTT",
            QuizDifficulty.Easy,
            [
                ("Biểu đồ UML nào thể hiện rõ nhất trình tự các bản tin trao đổi theo trục thời gian giữa Trình duyệt, SignalR Hub, AI Service và CSDL?", "Class Diagram", "Sequence Diagram (Biểu đồ Tuần tự)", "Pie Chart", "Use Case Diagram", "B", "Sequence Diagram mô tả chính xác vòng đời thông điệp đồng bộ/bất đồng bộ giữa các thành phần theo trình tự thời gian."),
                ("Trong thiết kế lược đồ cơ sở dữ liệu quan hệ chuẩn 3NF, bảng trung gian RoomMembers liên kết giữa Users và StudyRooms dùng để biểu diễn mối quan hệ nào?", "Quan hệ 1 - 1", "Quan hệ Nhiều - Nhiều (Many-to-Many) kèm thuộc tính vai trò RoleInRoom", "Không có quan hệ", "Đệ quy đơn", "B", "Một sinh viên tham gia nhiều phòng và một phòng có nhiều sinh viên, được tách thành hai quan hệ 1-Nhiều qua bảng RoomMembers."),
                ("Vì sao mọi phản hồi REST API trong dự án chuyên nghiệp nên thống nhất bọc trong một cấu trúc phong bì chung (như ApiResponse<T>)?", "Để làm chậm tốc độ tải", "Giúp tầng Frontend xử lý đồng nhất kết quả thành công, thông báo lỗi nghiệp vụ và danh sách lỗi kiểm tra đầu vào ở một nơi duy nhất", "Để thay thế giao thức HTTP", "Vì trình duyệt bắt buộc", "B", "HttpClient ở Frontend chỉ cần đọc trường success, message, data mà không phải đoán cấu trúc riêng lẻ từng API."),
                ("Khi trình diễn sản phẩm trước Hội đồng, cách xử lý nào chuyên nghiệp nhất cho các khóa bí mật (như Gemini API Key, SMTP App Password)?", "Viết cứng trực tiếp vào file HTML giao diện", "Đặt trong tệp cấu hình môi trường phía máy chủ (.env / appsettings.Local.json) và loại trừ khỏi Git qua .gitignore", "Đặt làm tiêu đề trang web", "Gửi kèm trong URL trình duyệt", "B", "Tuyệt đối tách biệt cấu hình bí mật khỏi mã nguồn công khai và không bao giờ phơi bày khóa hạ tầng lên trình duyệt."),
                ("Lợi ích lớn nhất của việc tách tầng Frontend theo mô hình *.api.js — *.view.js — *.controller.js là gì?", "Tăng số lượng dòng lệnh", "Tách bạch rõ giao tiếp mạng, hiển thị DOM an toàn chống XSS và điều phối sự kiện giúp mã nguồn sạch, dễ hiểu và dễ bảo vệ", "Không cần dùng CSS", "Tự động dịch sang C#", "B", "Mỗi tệp đảm nhận đúng 1 vai trò rõ ràng theo nguyên lý Single Responsibility Principle.")
            ],
            "Bộ thẻ Cẩm nang Phản biện Đồ án Xuất sắc",
            [
                ("Vai trò của GlobalExceptionMiddleware ở tầng API?", "Bắt tập trung mọi ngoại lệ (Validation 400, Unauthorized 401, Forbidden 403, NotFound 404) và trả về JSON ApiResponse chuẩn xác không lộ StackTrace.", 2),
                ("Giải thích ngắn gọn cơ chế Spaced Repetition 3 mức trên Flashcards?", "Phân loại thẻ theo mức nhớ (0: Chưa thuộc, 1: Tạm nhớ, 2: Đã thuộc) giúp sinh viên tập trung ôn kỹ các thẻ mức 0 và 1.", 2),
                ("Cách tính điểm tổng hợp trên Bảng xếp hạng Phòng học (Leaderboard)?", "Kết hợp Điểm trung bình Quiz (trọng số cao), Số đề hoàn thành và Mức độ tích cực trao đổi học thuật trong phòng.", 2),
                ("Tác dụng của View Transitions API trên ứng dụng Web hiện đại?", "Tạo chuyển cảnh mượt mà giữa các trạng thái DOM như một ứng dụng SPA cao cấp mà không cần cài đặt Framework nặng.", 1),
                ("Ba tiêu chí cốt lõi trong nguyên tắc Clean Code?", "SOLID (Thiết kế hướng đối tượng bền vững), DRY (Không lặp lại logic trùng lặp) và KISS (Giữ giải pháp rõ ràng, mạch lạc).", 2)
            ]),

        // 22. BLK412 (Pending - Phòng thứ 2 chờ Admin phê duyệt trên cổng Quản trị!)
        new(
            "BLK412",
            "Công nghệ Chuỗi khối (Blockchain), Cây băm Merkle & Hợp đồng Thông minh",
            "An toàn thông tin",
            "Cấu trúc khối liên kết băm SHA-256, cây Merkle xác minh giao dịch nhẹ SPV, cơ chế đồng thuận PoW vs PoS và bảo mật Smart Contracts.",
            RoomStatus.Pending, 1,
            [17, 5, 11, 15, 19],
            "vku-blk412-blockchain-merkle-smartcontracts.pdf",
            "Nền tảng Công nghệ Blockchain: Hàm băm Mật mã SHA-256, Cây Merkle & Smart Contracts",
            19,
            [
                "## SECTION 1: Tinh Bat bien cua Chuoi Khoi va Cau truc Cay Bam Nhi phan Merkle Tree",
                "Moi khoi (Block) luu ma bam cua khoi truoc do (Previous Block Hash) tao thanh day xich bat bien. Toan bo giao dich trong khoi duoc bam gop theo cap len dinh Merkle Root giup xac minh su ton tai cua 1 giao dich chi trong O(log2 N) phep bam.",
                "## SECTION 2: Dong thuan Proof-of-Work vs Proof-of-Stake va Loi Reentrancy trong Smart Contract",
                "Chuyen tu PoW sang PoS giam 99.95% dien nang tieu thu. Khi lap trinh Smart Contract, can ap dung mau Checks-Effects-Interactions de ngan chan tan cong goi đệ quy rut tien Reentrancy."
            ],
            "### Tóm tắt Học thuật: Cây Băm Merkle & Bảo mật Smart Contract\n" +
            "- **Độ phức tạp bằng chứng *Merkle Proof* cho khối có $N$ giao dịch:** Chỉ cần truyền **$\\lceil \\log_2 N \\rceil$** giá trị băm trung gian thay vì tải cả khối.\n" +
            "- **Mẫu phòng thủ *Reentrancy Attack*:** `Checks` (Kiểm tra điều kiện) $\\longrightarrow$ `Effects` (Trừ số dư nội bộ trước) $\\longrightarrow$ `Interactions` (Gọi chuyển tiền ra ngoài sau cùng).",
            "@bot Giải thích vì sao Cây Merkle giúp ví nhẹ trên điện thoại kiểm tra 1 giao dịch nằm trong khối gồm 1 triệu giao dịch chỉ với khoảng 20 mã băm?",
            "### Sức mạnh Logarit của `Merkle Proof` ($O(\\log_2 N)$)\n\n" +
            "- Cây Merkle ghép đôi và băm nhị phân từ lá lên gốc `Merkle Root`. Với $N = 1.048.576 = 2^{20}$ giao dịch, chiều cao cây chỉ đúng **20 tầng**.\n" +
            "- Thiết bị di động chỉ cần giữ tiêu đề khối (80 bytes) kèm **20 mã băm nhánh bên cạnh** ($20 \\times 32\\text{ bytes} = 640\\text{ bytes}$) để tự tính lại gốc và xác thực giao dịch chính xác 100%!",
            "Kiểm tra Công nghệ Blockchain, Cây Merkle & Smart Contracts",
            QuizDifficulty.Medium,
            [
                ("Để xác minh một giao dịch thuộc khối chứa N = 1024 giao dịch bằng bằng chứng Merkle Proof, nút nhẹ (Light Client) cần nhận bao nhiêu giá trị băm nhánh kề?", "1024 giá trị băm", "10 giá trị băm (vì log2(1024) = 10)", "512 giá trị băm", "100 giá trị băm", "B", "Chiều cao của cây nhị phân cân bằng 1024 lá là log2(1024) = 10, mỗi tầng chỉ cần 1 mã băm anh em để băm ngược lên Merkle Root."),
                ("Quy tắc lập trình an toàn nào trong Smart Contract giúp ngăn chặn triệt để lỗ hổng rút tiền lặp đệ quy (Reentrancy Attack)?", "Gửi tiền trước rồi mới kiểm tra số dư", "Checks - Effects - Interactions (Kiểm tra điều kiện -> Cập nhật giảm số dư trạng thái -> Cuối cùng mới chuyển tiền ra hợp đồng ngoài)", "Không dùng hàm kiểm tra", "Lưu khóa bí mật công khai trên chuỗi", "B", "Cập nhật số dư về 0 trước khi gọi ra ngoài đảm bảo nếu hợp đồng độc hại gọi ngược lại ngay lập tức thì điều kiện kiểm tra số dư sẽ thất bại.")
            ],
            "Bộ thẻ Blockchain & Merkle Tree",
            [
                ("Vì sao sửa đổi 1 ký tự trong khối cũ sẽ làm mất hiệu lực toàn bộ các khối phía sau?", "Hiệu ứng thác đổ (Avalanche Effect) của hàm băm SHA-256 làm đổi Hash khối đó, kéo theo sai lệch trường PreviousHash của mọi khối kế tiếp.", 2),
                ("Độ phức tạp không gian và thời gian của bằng chứng Merkle Proof?", "O(log2 N) với N là tổng số giao dịch trong khối.", 2)
            ])
    ];
}
