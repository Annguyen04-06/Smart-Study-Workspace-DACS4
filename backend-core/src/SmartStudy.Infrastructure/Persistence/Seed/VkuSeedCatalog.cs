using SmartStudy.Domain.Entities;
using SmartStudy.Domain.Enums;

namespace SmartStudy.Infrastructure.Persistence.Seed;

/// <summary>
/// Kho nội dung học thuật phong phú dành cho sinh viên CNTT trường VKU (8 giáo trình PDF, 6 đề Quiz x 10 câu, 6 bộ Flashcard).
/// </summary>
internal static class VkuSeedCatalog
{
    internal sealed record SeedMaterialItem(
        int RoomIndex,
        int UploaderIndex,
        string FileName,
        string CourseCode,
        string Title,
        int LogicalPageCount,
        string[] Sections,
        string AiSummary);

    public static List<SeedMaterialItem> GetMaterials() =>
    [
        // 1. NET101 - Tài liệu 1
        new(0, 0, "vku-net101-signalr-websockets.pdf", "NET101",
            "Chuyên đề 1: Kiến trúc WebSockets, Long Polling & ASP.NET Core SignalR",
            22,
            [
                "## SECTION 1: Tong quan Giao thuc Truyen thong Thoi gian thuc",
                "Trong cac ung dung phan tan hien dai nhu he thong phong hoc truc tuyen, san giao dich tai chinh va tro choi da nguoi choi, mo hinh Request-Response truyen thong cua HTTP/1.1 boc lo han che lon ve do tre (latency) va chi phi tieu thu bang thong Header.",
                "Giao thuc WebSocket (RFC 6455) khoi dau bang buoc bat tay HTTP Upgrade (HTTP 101 Switching Protocols) tren cung cong 80/443, sau do thiet lap kenh truyen song cong toan phan (Full-Duplex) duy tri ket noi TCP ben vung giua Client va Server.",
                "## SECTION 2: Co che Transport Fallback trong ASP.NET Core SignalR",
                "ASP.NET Core SignalR cung cap lop truu tuong RPC hai chieu tren nen 3 giao thuc van chuyen theo thu tu uu tien: (1) WebSockets la toi uu nhat voi do tre duoi 10ms; (2) Server-Sent Events (SSE) truyen mot chieu tu Server ve trinh duyet qua text/event-stream; (3) HTTP Long Polling danh cho moi truong Proxy hoac Firewall chan nang cap ket noi.",
                "## SECTION 3: Quan ly Groups, Presence Tracker va Xac thuc JWT",
                "Khac voi REST API gui Bearer Token qua HTTP Header Authorization, trinh duyet WebSocket API khong ho tro dinh kem tuy chinh Header khi bat tay. Do do, SignalR truyen chuoi JWT qua tham so truy van ?access_token=... va duoc doc tai su kien JwtBearerEvents.OnMessageReceived.",
                "Khi xu ly tac vu bat dong bo keo dai nhu goi dich vu AI (@bot), Hub co vong doi Transient cho tung loi goi RPC. Lap trinh vien bat buoc su dung IServiceScopeFactory de tao Scope doc lap lay DbContext moi, tuyet doi khong tai su dung DbContext cua Hub sau khi phuong thuc ket thuc de tranh loi ObjectDisposedException."
            ],
            "### Tóm tắt Học thuật: WebSockets & ASP.NET Core SignalR\n\n" +
            "| Cơ chế truyền tải | Chiều dữ liệu | Độ trễ trung bình | Đặc điểm vận hành |\n" +
            "| :--- | :--- | :--- | :--- |\n" +
            "| **WebSockets (RFC 6455)** | Song công toàn phần (*Full-Duplex*) | $< 10\\text{ ms}$ | Nâng cấp qua `HTTP 101`, kết nối TCP bền vững |\n" +
            "| **Server-Sent Events** | Đơn công (*Server $\\to$ Client*) | $15 - 40\\text{ ms}$ | Chuẩn HTML5 `EventSource`, tự động kết nối lại |\n" +
            "| **HTTP Long Polling** | Giả lập thời gian thực | $> 120\\text{ ms}$ | Tương thích mọi tường lửa doanh nghiệp |\n\n" +
            "- **Xác thực JWT trên WebSockets:** Đọc `access_token` từ Query String trong `JwtBearerEvents.OnMessageReceived`.\n" +
            "- **An toàn đa luồng:** Dùng `ConcurrentDictionary` cho `PresenceTracker` và `IServiceScopeFactory` cho tác vụ nền."),

        // 2. NET101 - Tài liệu 2
        new(0, 2, "vku-net101-tcp-udp-congestion.pdf", "NET101",
            "Chuyên đề 2: Phân tích Giao thức Tầng Giao vận TCP, UDP & Kiểm soát Tắc nghẽn",
            19,
            [
                "## SECTION 1: So sanh Kien truc TCP va UDP tai Tang Giao van",
                "Giao thuc TCP (Transmission Control Protocol - RFC 793) la giao thuc huong ket noi (Connection-Oriented), dam bao truyen tin cay theo dung thu tu byte nho co che danh so thu tu Sequence Number, xac nhan tich luy ACK va truyen lai khi mat goi.",
                "Nguoc lai, UDP (User Datagram Protocol - RFC 768) la giao thuc phi ket noi (Connectionless) voi kich thuoc Header co dinh chi 8 bytes (so voi 20-60 bytes cua TCP), phu hop cho truyen phat am thanh VoIP, WebRTC, DNS va DNS-over-QUIC.",
                "## SECTION 2: Bat tay 3 buoc (3-Way Handshake) va Dong ket noi 4 buoc",
                "Qua trinh thiet lap ket noi TCP dien ra qua 3 goi tin: (1) Client gui SYN(seq=x); (2) Server phan hoi SYN-ACK(seq=y, ack=x+1); (3) Client xac nhan ACK(ack=y+1). Trang thai TIME_WAIT keo dai 2*MSL giup bao dam goi ACK cuoi cung den duoc phia nhan va loai bo cac phan doan tre tren mang.",
                "## SECTION 3: Kiem soat Luong (Flow Control) va Kiem soat Tac nghen (Congestion Control)",
                "TCP su dung cua so truot nhan rwnd (Receive Window) de tranh tran bo dem phia nhan, ket hop cua so tac nghen cwnd voi 4 giai doan kinh dien: Slow Start (tang mu sau moi RTT), Congestion Avoidance (tang tuyen tinh AIMD), Fast Retransmit (khi nhan 3 Duplicate ACKs) va Fast Recovery."
            ],
            "### Tóm tắt Học thuật: TCP, UDP & Kiểm soát Tắc nghẽn\n" +
            "- **Kích thước Header:** `UDP` chỉ tốn **8 bytes**, trong khi `TCP` tốn tối thiểu **20 bytes**.\n" +
            "- **Thông lượng hiệu dụng TCP:** Phụ thuộc vào kích thước cửa sổ thực tế:\n" +
            "$$W_{\\text{effective}} = \\min(\\text{rwnd}, \\text{cwnd})$$\n" +
            "- **Ước lượng RTT làm mịn (EWMA):**\n" +
            "$$\\text{EstimatedRTT} = (1 - \\alpha)\\cdot\\text{EstimatedRTT} + \\alpha\\cdot\\text{SampleRTT} \\quad (\\alpha = 0.125)$$"),

        // 3. DSA202 - Tài liệu 3
        new(1, 1, "vku-dsa202-graph-shortest-path.pdf", "DSA202",
            "Giáo trình Đồ thị Chuyên sâu: BFS, DFS, Dijkstra, Bellman-Ford & Cây khung Nhỏ nhất",
            26,
            [
                "## SECTION 1: Bieu dien Do thi va Duyet BFS / DFS",
                "Do thi G = (V, E) gom tap dinh V va tap canh E. Voi do thi thua (|E| << |V|^2), bieu dien bang Danh sach ke (Adjacency List) toi uu khong gian bo nho O(|V| + |E|), trong khi Ma tran ke (Adjacency Matrix) tieu ton O(|V|^2) nhung kiem tra su ton tai cua canh (u, v) trong O(1).",
                "Thuat toan BFS su dung hang doi FIFO kham pha do thi theo tung lop khoang cach, tim duong di it canh nhat trong O(|V| + |E|). Thuat toan DFS su dung de quy hoac Stack ho tro phat hien chu trinh, tim thanh phan lien thong manh (Tarjan/Kosaraju) va Sap xep To-po (Topological Sort) tren DAG.",
                "## SECTION 2: Bai toan Duong di Ngan nhat Nguon don (SSSP)",
                "Thuat toan Dijkstra ap dung chien luoc Tham lam (Greedy) cho do thi co trong so KHONG AM w(u,v) >= 0. Khi cai dat bang Binary Min-Heap, tong thoi gian thuc thi la O((|V| + |E|) log |V|). Neu ton tai canh am, ta bat buoc dung Bellman-Ford voi |V| - 1 vong thu gian canh ton O(|V| * |E|) va vong kiem tra thu |V| de phat hien chu trinh am.",
                "## SECTION 3: Cay khung Nho nhat (Minimum Spanning Tree - Prim & Kruskal)",
                "Thuat toan Kruskal sap xep danh sach canh tang dan theo trong so O(|E| log |E|) ket hop cau truc Disjoint Set Union (DSU / Union-Find voi Path Compression & Union by Rank) de kiem tra chu trinh trong thoi gian gan nhu hang so O(alpha(|V|))."
            ],
            "### Tóm tắt Học thuật: Giải thuật Đồ thị & Đường đi Ngắn nhất\n\n" +
            "| Thuật toán | Trọng số âm | Độ phức tạp Thời gian | Cấu trúc dữ liệu cốt lõi |\n" +
            "| :--- | :---: | :--- | :--- |\n" +
            "| **BFS** | Không trọng số | $O(|V| + |E|)$ | Hàng đợi `Queue` (FIFO) |\n" +
            "| **Dijkstra** | Không ($w \\ge 0$) | $O((|V| + |E|)\\log|V|)$ | Hàng đợi ưu tiên `Min-Heap` |\n" +
            "| **Bellman-Ford** | Có (bắt chu trình âm) | $O(|V|\\cdot|E|)$ | Mảng khoảng cách `dist[]` |\n" +
            "| **Kruskal (MST)** | Có | $O(|E|\\log|E|)$ | `Disjoint Set Union (DSU)` |"),

        // 4. DSA202 - Tài liệu 4
        new(1, 3, "vku-dsa202-dynamic-programming.pdf", "DSA202",
            "Chuyên đề Quy hoạch Động (Dynamic Programming): Balo, LCS & Tối ưu Trạng thái",
            21,
            [
                "## SECTION 1: Hai Tinh chat Cot loi cua Quy hoach Dong",
                "Quy hoach dong giai quyet bai toan toi uu bang cach phan ra thanh cac bai toan con giao nhau (Overlapping Subproblems) va thoa man tinh chat Cau truc con Toi uu (Optimal Substructure). Hai phuong phap trien khai chinh gom Top-Down (De quy co nho Memoization) va Bottom-Up (Lap bang Tabulation).",
                "## SECTION 2: Bai toan Cai tui 0/1 Knapsack va Day con Chung Dai nhat (LCS)",
                "Voi bai toan Cai tui 0/1 gom N vat va trong luong toi da W, cong thuc truy hoi la dp[i][w] = max(dp[i-1][w], dp[i-1][w - weight[i]] + value[i]) voi do phuc tap O(N * W). Co the toi uu khong gian tu mang 2 chieu xuong mang 1 chieu O(W) bang cach duyet w giam dan tu W ve weight[i].",
                "Voi bai toan Day con chung dai nhat (Longest Common Subsequence - LCS) cua hai xau X do dai m va Y do dai n: neu X[i] == Y[j] thi dp[i][j] = dp[i-1][j-1] + 1; nguoc lai dp[i][j] = max(dp[i-1][j], dp[i][j-1]) voi do phuc tap O(m * n)."
            ],
            "### Tóm tắt Học thuật: Quy hoạch Động (Dynamic Programming)\n" +
            "- **Điều kiện áp dụng:** *Optimal Substructure* + *Overlapping Subproblems*.\n" +
            "- **Công thức truy hồi Balo 0/1 (*0/1 Knapsack*):**\n" +
            "$$dp[i][w] = \\max\\big(dp[i-1][w],\\; dp[i-1][w - w_i] + v_i\\big)$$\n" +
            "- **Tối ưu bộ nhớ 1 chiều:** Duyệt ngược $w$ từ $W \\to w_i$ để mỗi vật chỉ được chọn tối đa 1 lần."),

        // 5. AI305 - Tài liệu 5
        new(2, 4, "vku-ai305-machine-learning-foundations.pdf", "AI305",
            "Nhập môn Trí tuệ Nhân tạo: Tìm kiếm A*, Định lý Bayes & Thuật toán Gradient Descent",
            24,
            [
                "## SECTION 1: Tim kiem Thong minh A* (A-Star Search) va Tinh chap nhan duoc",
                "Thuat toan A* danh gia moi trang thai n thong qua ham chi phi f(n) = g(n) + h(n), trong do g(n) la chi phi thuc te tu trang thai dau den n va h(n) la ham uoc luong Heuristic den dich. A* dam bao tim ra nghiem toi uu toan cuc neu ham h(n) thoa tinh Chap nhan duoc (Admissible: 0 <= h(n) <= h*(n)) va Nhat quan (Consistent / Monotonic).",
                "## SECTION 2: Suy dien Xac suat Bayes va Bo phan loai Naive Bayes",
                "Dinh ly Bayes cho phep cap nhat xac suat hau nghiem P(Y|X) dua tren xac suat tien nghiem P(Y) va ham hop ly P(X|Y). Bo phan loai Naive Bayes gia dinh cac dac trung x_1, x_2, ..., x_d doc lap co dieu kien khi biet nhan lop Y, ket hop ky thuat Lam min Laplace (Laplace Smoothing) de tranh xac suat bang 0.",
                "## SECTION 3: Hoi quy Tuyen tinh, Logistic Regression va Gradient Descent",
                "Thuat toan Ha doc (Gradient Descent) cuc tieu hoa ham mat mat J(theta) bang cach cap nhat tham so nguoc chieu dao ham rieng voi toc do hoc (Learning Rate) alpha. Neu alpha qua nho hoi tu cham; neu alpha qua lon ham mat mat se dao dong hoac phan ky. Ky thuat Regularization L2 (Ridge) va L1 (Lasso) giup chong hien tuong Qua khop (Overfitting)."
            ],
            "### Tóm tắt Học thuật: Tìm kiếm A*, Định lý Bayes & Học máy\n" +
            "- **Hàm đánh giá $A^*$:** $f(n) = g(n) + h(n)$ tối ưu khi $0 \\le h(n) \\le h^*(n)$ (*Admissible*).\n" +
            "- **Định lý Bayes nền tảng:**\n" +
            "$$P(A|B) = \\frac{P(B|A)\\cdot P(A)}{P(B)}$$\n" +
            "- **Quy tắc cập nhật Gradient Descent:**\n" +
            "$$\\theta_j := \\theta_j - \\alpha \\frac{\\partial J(\\theta)}{\\partial \\theta_j}$$"),

        // 6. SQL204 - Tài liệu 6
        new(3, 5, "vku-sql204-indexing-acid-optimization.pdf", "SQL204",
            "Tối ưu hóa Truy vấn SQL Server: Cấu trúc B+Tree Index, Execution Plan & Giao dịch ACID",
            20,
            [
                "## SECTION 1: Chuan hoa Luoc do 3NF va BCNF trong Thiet ke CSDL Quan he",
                "Mot luoc do quan he dat Chuan 3 (3NF) khi no dat 2NF va moi thuoc tinh khong khoa khong phu thuoc bac cau vao khoa chinh. Viec thiet ke chuan 3NF giup loai bo di thuong khi chen, sua, xoa va tiet kiem dung luong luu tru tren Data Pages 8KB cua SQL Server.",
                "## SECTION 2: Cau truc Chi muc B+Tree - Clustered vs Non-Clustered Index",
                "Moi bang trong SQL Server chi co toi da 1 Clustered Index vi cac nut la (Leaf Pages) cua Clustered Index chinh la du lieu vat ly cua bang duoc sap xep theo khoa chi muc. Nguoc lai, mot bang co the co nhieu Non-Clustered Index; nut la cua chung chua con tro Row Locator (khoa Clustered). Su dung menh de INCLUDE giup tao Covering Index, triet tieu thao tac Key Lookup ton kem.",
                "## SECTION 3: Giao dich ACID, Isolation Levels va Phong chong Deadlock",
                "Bon tinh chat ACID gom Atomicity, Consistency, Isolation va Durability (nho Write-Ahead Logging - WAL). SQL Server ho tro Read Uncommitted, Read Committed (mac dinh), Snapshot Isolation (MVCC trong tempdb), Repeatable Read va Serializable."
            ],
            "### Tóm tắt Học thuật: Tối ưu Truy vấn & B+Tree Index trên SQL Server\n\n" +
            "| Tiêu chí | Clustered Index | Non-Clustered Index |\n" +
            "| :--- | :--- | :--- |\n" +
            "| **Số lượng tối đa/bảng** | Duy nhất **1** | Lên tới **999** chỉ mục |\n" +
            "| **Dữ liệu tại nút lá** | Toàn bộ bản ghi vật lý (Data Pages) | Khóa chỉ mục + Con trỏ `Key/RID` |\n" +
            "| **Tối ưu truy vấn** | `BETWEEN`, `ORDER BY`, khóa chính `Id` | Điều kiện lọc chọn lọc cao + `INCLUDE` |\n\n" +
            "- **Quy tắc SARGable:** Không bọc hàm quanh cột có Index ở mệnh đề `WHERE` (ví dụ tránh `YEAR(CreatedAt) = 2026`, thay bằng khoảng thời gian `>=` và `<`)."),

        // 7. MAT108 - Tài liệu 7
        new(4, 6, "vku-mat108-discrete-math-combinatorics.pdf", "MAT108",
            "Toán Rời rạc Ôn thi Cuối kỳ: Tổ hợp, Nguyên lý Bù trừ, Hệ thức Truy hồi & Chu trình Euler",
            18,
            [
                "## SECTION 1: Nguyen ly Dem, Dirichlet (Chuong bo cau) va Bao ham - Loai tru",
                "Nguyen ly Dirichlet khang dinh neu xep n vat vao k hop voi n > k thi ton tai it nhat mot hop chua tu ceil(n/k) vat tro len. Nguyen ly Bao ham - Loai tru (Inclusion-Exclusion) tinh luc luong hop cua nhieu tap hop giao nhau: |A U B U C| = |A| + |B| + |C| - |A n B| - |B n C| - |C n A| + |A n B n C|.",
                "## SECTION 2: Giai He thuc Truy hoi Tuyen tinh va Dinh ly Master",
                "He thuc truy hoi tuyen tinh thuan nhat bac 2 dang a_n = c_1*a_{n-1} + c_2*a_{n-2} duoc giai qua phuong trinh dac trung r^2 - c_1*r - c_2 = 0. Dinh ly Master uoc luong do phuc tap thuat toan Chia de tri T(n) = a*T(n/b) + f(n) so sanh f(n) voi n^(log_b(a)).",
                "## SECTION 3: Do thi Euler va Do thi Hamilton",
                "Do thi vo huong lien thong G co Chu trinh Euler (di qua moi canh dung 1 lan roi quay ve dinh xuat phat) khi va chi khi MOI dinh cua G deu co bac chan. G co Duong di Euler khi va chi khi G co dung 0 hoac 2 dinh bac le."
            ],
            "### Tóm tắt Học thuật: Toán Rời rạc & Tổ hợp Đồ thị\n" +
            "- **Nguyên lý Chuồng bồ câu (*Pigeonhole Principle*):** $N$ đối tượng vào $k$ hộp $\\Rightarrow$ có ít nhất 1 hộp chứa $\\lceil N/k \\rceil$ đối tượng.\n" +
            "- **Định lý Master cho Chia để trị $T(n) = aT(n/b) + \\Theta(n^d)$:**\n" +
            "$$T(n) = \\begin{cases} \\Theta(n^d) & \\text{nếu } a < b^d \\\\ \\Theta(n^d \\log n) & \\text{nếu } a = b^d \\\\ \\Theta(n^{\\log_b a}) & \\text{nếu } a > b^d \\end{cases}$$\n" +
            "- **Điều kiện cần & đủ Chu trình Euler:** Đồ thị vô hướng liên thông có **tất cả các đỉnh đều bậc chẵn**."),

        // 8. OS209 - Tài liệu 8
        new(5, 7, "vku-os209-linux-process-deadlock.pdf", "OS209",
            "Nguyên lý Hệ điều hành Linux: Lập lịch Tiến trình CPU, Semaphore & Phòng chống Deadlock",
            20,
            [
                "## SECTION 1: Tien trinh (Process), Luong (Thread) va Loi goi He thong fork/exec",
                "Trong nhan Linux, moi tien trinh duoc bieu dien boi cau truc task_struct. Loi goi he thong fork() nhan ban tien trinh con voi co che Copy-on-Write (CoW) giup khong phai sao chep ngay toan bo trang bo nho vat ly cho den khi co thao tac ghi.",
                "## SECTION 2: Cac Thuat toan Lap lich CPU va Completely Fair Scheduler (CFS)",
                "Cac giai thuat co dien gom FCFS, SJF / SRTF (toi uu thoi gian cho trung binh), Priority Scheduling va Round Robin (chia lat thoi gian Quantum q). Nhan Linux hien dai su dung CFS luu tru cac tien trinh trong Cay Do-Den (Red-Black Tree) sap xep theo vruntime nho nhat.",
                "## SECTION 3: Dong bo hoa Vung gang (Critical Section) va 4 Dieu kien Coffman gay Deadlock",
                "Deadlock chi xay ra khi dong thoi thoa man ca 4 dieu kien Coffman: (1) Mutual Exclusion; (2) Hold and Wait; (3) No Preemption; (4) Circular Wait. Thuat toan Nha bang (Banker's Algorithm) cua Dijkstra kiem tra trang thai An toan (Safe State) truoc khi cap phat tai nguyen."
            ],
            "### Tóm tắt Học thuật: Tiến trình Linux, Lập lịch CPU & Deadlock\n" +
            "- **Cơ chế `fork()` trên Linux:** Áp dụng *Copy-on-Write (CoW)* giúp tạo tiến trình con cực nhanh.\n" +
            "- **Bộ lập lịch CFS (*Completely Fair Scheduler*):** Chọn tiến trình có `vruntime` nhỏ nhất bằng **Cây Đỏ-Đen** với thao tác $O(\\log N)$.\n" +
            "- **4 Điều kiện Coffman gây Deadlock:** *Mutual Exclusion*, *Hold & Wait*, *No Preemption*, *Circular Wait* — chỉ cần bẻ gãy 1 trong 4 điều kiện sẽ ngăn chặn hoàn toàn tắc nghẽn."),

        // 9. WEB301 - Tài liệu 9
        new(6, 2, "vku-web301-restful-jwt-architecture.pdf", "WEB301",
            "Kiến trúc Ứng dụng Web Hiện đại: Chuẩn RESTful API, Xác thực JWT, CORS & Tối ưu HTTP/2",
            23,
            [
                "## SECTION 1: Nguyen tac Thiet ke RESTful API va Tinh Luy dang (Idempotency)",
                "Kien truc REST su dung danh tu so nhieu cho URI tai nguyen (vi du /api/rooms, /api/quizzes) ket hop ngu nghia HTTP Methods. Cac phuong thuc GET, PUT, DELETE co tinh Luy dang (Idempotent: goi nhieu lan cung cho trang thai cuoi nhu goi 1 lan), trong khi POST tao tai nguyen moi va tra ve HTTP 201 Created.",
                "## SECTION 2: Cau truc JSON Web Token (JWT - RFC 7519) va Luong Xac thuc Stateless",
                "Mot chuoi JWT gom 3 phan Header.Payload.Signature ma hoa Base64Url va ky so bang HMAC-SHA256 hoac RSA. Do Server khong can luu Session trong RAM (Stateless), ung dung de dang mo rong ngang (Horizontal Scaling) qua Load Balancer.",
                "## SECTION 3: Co che Same-Origin Policy, CORS Preflight va Bo nho dem ETag",
                "Khi Frontend goi API khac Origin kem Authorization Header, trinh duyet tu dong gui goi OPTIONS (CORS Preflight) kiem tra Access-Control-Allow-Origin. Ky thuat ETag va HTTP 304 Not Modified giup giam 80% bang thong tai tai nguyen tinh."
            ],
            "### Tóm tắt Học thuật: RESTful API, JWT Stateless & CORS\n\n" +
            "| HTTP Method | Mục đích Nghiệp vụ | Tính Lũy đẳng (*Idempotent*) | Mã Phản hồi Chuẩn |\n" +
            "| :--- | :--- | :---: | :---: |\n" +
            "| `GET` | Truy vấn danh sách / chi tiết | Có | `200 OK` |\n" +
            "| `POST` | Khởi tạo tài nguyên mới | Không | `201 Created` |\n" +
            "| `PUT` | Cập nhật toàn phần tài nguyên | Có | `200 OK` |\n" +
            "| `DELETE` | Xóa tài nguyên xác định | Có | `200 OK` / `204` |\n\n" +
            "- **Cấu trúc JWT:** `Base64Url(Header).Base64Url(Payload).HMACSHA256(Secret)`."),

        // 10. SEC308 - Tài liệu 10
        new(7, 3, "vku-sec308-cryptography-owasp-security.pdf", "SEC308",
            "An toàn Thông tin & Mật mã học: Hệ mã RSA, Hàm băm BCrypt & Phòng chống OWASP Top 10",
            25,
            [
                "## SECTION 1: Mat ma Khoa Doi xung AES va Khoa Bat doi xung RSA",
                "He ma bat doi xung RSA dua tren do kho cua bai toan phan tich so nguyen lon n = p * q ra thua so nguyen to. Voi phi(n) = (p-1)*(q-1), chon so mu cong khai e nguyen to cung nhau voi phi(n) va tinh khoa bi mat d sao cho e * d dong du 1 (mod phi(n)). Ban ma C = M^e mod n va giai ma M = C^d mod n.",
                "## SECTION 2: Bam Mat khau Mot chieu voi Salt trong BCrypt / Argon2",
                "Tuyet doi khong luu mat khau dang ro hoac bam nhanh MD5/SHA-1 vi de bi tan cong Rainbow Table. BCrypt tu dong sinh Salt ngau nhien 128-bit cho tung nguoi dung va cho phep tang he so chi phi Work Factor (2^cost vong lap) de chong tan cong vet can GPU.",
                "## SECTION 3: Phong chong SQL Injection, Stored XSS va CSRF theo OWASP Top 10",
                "SQL Injection bi triet tieu hoan toan khi dung Parameterized Queries hoac ORM nhu EF Core LINQ. Tan cong Cross-Site Scripting (XSS) duoc ngan chan bang cach ma hoa HTML Output (escapeHtml) moi du lieu do nguoi dung nhap truoc khi gan vao DOM."
            ],
            "### Tóm tắt Học thuật: Mật mã RSA, BCrypt & Bảo mật Web OWASP\n" +
            "- **Hệ mật mã khóa công khai RSA:** Với $n = p\\cdot q$ và $\\varphi(n) = (p-1)(q-1)$:\n" +
            "$$C = M^e \\bmod n, \\qquad M = C^d \\bmod n \\quad (e\\cdot d \\equiv 1 \\pmod{\\varphi(n)})$$\n" +
            "- **Lưu trữ mật khẩu an toàn:** Sử dụng `BCrypt` tích hợp *Random Salt* và *Adaptive Cost Factor*.\n" +
            "- **Chống lỗ hổng OWASP:** Dùng *Parameterized Queries* chống `SQLi` và `escapeHtml()` chống `Stored XSS`."),

        // 11. CLD405 - Tài liệu 11
        new(8, 8, "vku-cld405-docker-kubernetes-cicd.pdf", "CLD405",
            "Điện toán Đám mây & DevOps: Kiến trúc Docker Multi-Stage, Microservices & Đường ống CI/CD",
            21,
            [
                "## SECTION 1: Ao hoa Container voi Linux Namespaces va Control Groups (cgroups)",
                "Khac voi May ao (Virtual Machine) phai khoi dong toan bo Guest OS nang hang GB, Docker Container chia se truc tiep Host Linux Kernel, su dung Namespaces de co lap PID/Network/Mount va cgroups de gioi han dinh muc CPU/RAM, khoi dong chi trong vai tram mili-giay.",
                "## SECTION 2: Ky thuat Multi-Stage Dockerfile va Docker Compose Networking",
                "Multi-Stage Build tach rieng Stage SDK (bien dich ma nguon .NET / Go / TypeScript) va Stage Runtime toi gian (Alpine / Distroless), giup giam kich thuoc Docker Image tu 850MB xuong duoi 110MB va thu hep be mat tan cong bao mat.",
                "## SECTION 3: Duong ong Tu dong hoa CI/CD va Trien khai Zero-Downtime",
                "Quy trinh CI/CD tu dong chay kiem thu don vi (Unit Tests), phan tich tinh ma nguon, dong goi Image va trien khai theo chien luoc Rolling Update hoac Blue-Green Deployment ket hop Health Check Endpoint (/health) de khong gay gian doan dich vu."
            ],
            "### Tóm tắt Học thuật: Docker Container, Multi-Stage Build & CI/CD\n\n" +
            "| Đặc trưng Kỹ thuật | Máy ảo (*Virtual Machine*) | Docker Container |\n" +
            "| :--- | :--- | :--- |\n" +
            "| **Mức độ cô lập** | Phần cứng ảo hóa + Guest OS riêng | Dùng chung Kernel (`Namespaces` + `cgroups`) |\n" +
            "| **Dung lượng ảnh** | $2\\text{ GB} - 15\\text{ GB}$ | $40\\text{ MB} - 180\\text{ MB}$ (*Multi-stage*) |\n" +
            "| **Thời gian khởi động** | $30 - 90$ giây | $< 1$ giây |\n\n" +
            "- **Zero-Downtime Deployment:** Kết hợp *Blue-Green* / *Rolling Update* với cổng kiểm tra sức khỏe `/api/v1/health`."),

        // 12. SWE206 - Tài liệu 12
        new(9, 9, "vku-swe206-clean-architecture-solid.pdf", "SWE206",
            "Công nghệ Phần mềm Hiện đại: Clean Architecture 4 Tầng, Nguyên lý SOLID & Kiểm thử xUnit",
            24,
            [
                "## SECTION 1: Nam Nguyen ly Thiet ke Huong doi tuong SOLID",
                "Bo nguyen ly SOLID gom: (S) Single Responsibility - moi lop chi co 1 ly do de thay doi; (O) Open/Closed - mo cho mo rong nhung dong cho sua doi; (L) Liskov Substitution; (I) Interface Segregation - tach giao dien nho chuyen biet; (D) Dependency Inversion - tang nghiep vu cao cap phu thuoc vao Abstraction chu khong phu thuoc chi tiet ha tang.",
                "## SECTION 2: Quy tac Phu thuoc (Dependency Rule) trong Clean Architecture",
                "He thong chia thanh 4 tang dong tam: Domain (Entities thuan khong phu thuoc thu vien ngoai) <- Application (Use Cases, DTOs, Validators, Interfaces) <- Infrastructure (EF Core SQL Server, SMTP, External HTTP Clients) va API (Thin Controllers, SignalR Hubs).",
                "## SECTION 3: Chien luoc Kiem thu Kim tu thap (Testing Pyramid) voi xUnit",
                "Tang day Kim tu thap la Unit Tests chiem 70-80% kiem tra nhanh logic nghiep vu bang InMemory / Test Doubles khong can mang ngoai; tang giua la Integration Tests kiem tra CSDL va API; tang dinh la End-to-End UI Tests."
            ],
            "### Tóm tắt Học thuật: Clean Architecture & Nguyên lý SOLID\n" +
            "- **Chiều phụ thuộc bất biến (*Strict Dependency Rule*):** `API / Infrastructure` $\\longrightarrow$ `Application` $\\longrightarrow$ `Domain`.\n" +
            "- **Nguyên lý Đảo ngược Phụ thuộc (`DIP`):** Tầng `Application` định nghĩa `IAppDbContext`, `IEmailSender`, `IAiMicroserviceClient`; tầng `Infrastructure` thực thi chi tiết.\n" +
            "- **Thin Controllers:** Controller chỉ tiếp nhận HTTP Request, ủy quyền xuống Service và trả về phong bì `ApiResponse<T>` đồng nhất."),

        // 13. IOT310 - Tài liệu 13 (Phòng Pending để Admin duyệt)
        new(10, 10, "vku-iot310-mqtt-esp32-embedded.pdf", "IOT310",
            "Hệ thống Nhúng & IoT Thông minh: Vi điều khiển ESP32, Giao thức Publish/Subscribe MQTT & QoS",
            19,
            [
                "## SECTION 1: Kien truc He thong IoT 4 Tang va Vi dieu khien ESP32",
                "Mot he thong IoT hoan chinh gom Tang Cam bien / Chap hanh (Perception Layer), Tang Truyen dan Mang (Network Layer: Wi-Fi, BLE, LoRaWAN), Tang Xu ly Bien / Dam may (Edge & Cloud Middleware) va Tang Ung dung giam sat thoi gian thuc.",
                "## SECTION 2: Giao thuc Nhe MQTT (Message Queuing Telemetry Transport)",
                "Khac voi HTTP nang ne, MQTT hoat dong theo mo hinh Publish/Subscribe qua trung gian MQTT Broker (Mosquitto / EMQX) voi kich thuoc goi tin toi thieu chi 2 bytes. MQTT cung cap 3 muc chat luong dich vu: QoS 0 (At most once), QoS 1 (At least once) va QoS 2 (Exactly once).",
                "## SECTION 3: Tiet kiem Nang luong Deep Sleep tren Nut Cam bien Pin",
                "Khi kich hoat che do Deep Sleep tren ESP32, CPU va Wi-Fi tat hoan toan, chi giu bo dinh thoi RTC hoat dong voi dong tieu thu ~10 micro-ampe, keo dai tuoi tho pin tu vai ngay len hang nam."
            ],
            "### Tóm tắt Học thuật: Giao thức IoT MQTT & Tiết kiệm Năng lượng ESP32\n" +
            "- **Mô hình MQTT Publish/Subscribe:** Thiết bị cảm biến và ứng dụng giao tiếp gián tiếp qua `MQTT Broker` theo cây chủ đề (*Topic Hierarchy*).\n" +
            "- **Ba mức Chất lượng Dịch vụ (`QoS`):** `QoS 0` (*Tối đa 1 lần*), `QoS 1` (*Ít nhất 1 lần*), `QoS 2` (*Đúng 1 lần duy nhất*).\n" +
            "- **Ước tính thời lượng pin thiết bị IoT:**\n" +
            "$$T_{\\text{hours}} = \\frac{C_{\\text{mAh}}}{I_{\\text{avg (mA)}}}, \\qquad I_{\\text{avg}} = d \\cdot I_{\\text{active}} + (1 - d) \\cdot I_{\\text{sleep}}$$")
    ];
}
