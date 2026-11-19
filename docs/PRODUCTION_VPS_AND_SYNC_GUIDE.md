# Hướng dẫn Triển khai VPS 24/7, Tự động HTTPS & Đồng bộ Giao diện OTA Auto-Sync

Tài liệu hướng dẫn từng bước đưa **Smart Study Workspace** lên máy chủ VPS Ubuntu chạy **24/7 độc lập với máy tính cá nhân**, tự động cấp chứng chỉ SSL HTTPS miễn phí, và kích hoạt cơ chế **OTA Auto-Sync** giúp mọi App PWA đã cài trên Laptop & Điện thoại tự động cập nhật giao diện mới nhất chỉ sau 1 câu lệnh.

---

## 1. Triển khai 1 Lệnh lên VPS Ubuntu/Debian (Tự động HTTPS 24/7)

### Yêu cầu tối thiểu VPS
- Nhà cung cấp bất kỳ: **Oracle Cloud Free Tier, DigitalOcean, Vietnix, Vultr, Hetzner, Azure**.
- Hệ điều hành: **Ubuntu 22.04 / 24.04 LTS** hoặc **Debian 12**.
- Cấu hình khuyên dùng: **2 vCPU / 2–4 GB RAM** (script `deploy-vps.sh` tự động tạo thêm **Swap 3 GB** nếu máy RAM thấp để SQL Server 2022 chạy ổn định).
- Tên miền trỏ bản ghi `A` về IP công khai của VPS *(có thể dùng tên miền miễn phí `.duckdns.org` hoặc `<IP-VPS>.nip.io` để Caddy tự cấp SSL Let's Encrypt miễn phí)*.

### Các bước thực hiện (chỉ mất ~3 phút)

```bash
# 1. SSH vào VPS Ubuntu và kéo mã nguồn về máy chủ
git clone <URL_REPO_GITHUB_CUA_BAN> /opt/smartstudy
cd /opt/smartstudy

# 2. Tạo file cấu hình môi trường Production và điền thông số thật
cp deploy/vps/.env.production.example deploy/vps/.env.production
nano deploy/vps/.env.production
```

Điền tối thiểu 5 biến trong `deploy/vps/.env.production`:
- `DOMAIN_NAME`: Tên miền trỏ về VPS (ví dụ `smartstudy.duckdns.org` hoặc `103.82.12.34.nip.io`; nếu chưa có tên miền dùng IP thuần thì ghi `:80`).
- `SQL_SA_PASSWORD`: Mật khẩu mạnh cho SQL Server 2022 (gồm chữ Hoa, thường, số, ký tự đặc biệt).
- `GEMINI_API_KEY`: Khóa Google Gemini API (`AIza...` hoặc `AQ....`).
- `JWT_SECRET`: Chuỗi bí mật ký JWT tối thiểu 32 ký tự.
- `INTERNAL_API_KEY`: Chuỗi khóa nội bộ giao tiếp C# $\leftrightarrow$ Python.

```bash
# 3. Cấp quyền và chạy đúng 1 lệnh triển khai toàn hệ thống 5 container
chmod +x deploy/vps/deploy-vps.sh deploy/vps/update-vps.sh
sudo ./deploy/vps/deploy-vps.sh
```

Script tự động thực hiện trọn gói:
1. Kiểm tra RAM và tạo **Swap 3 GB** nếu cần.
2. Cài đặt **Docker Engine & Docker Compose Plugin**.
3. Mở tường lửa UFW cổng `80` / `443`.
4. Khởi chạy 5 container chạy 24/7 với `restart: unless-stopped`:
   - `sqlserver` (SQL Server 2022 + volume dữ liệu vĩnh viễn)
   - `ai-service` (Python FastAPI + Gemini 2.5 Flash)
   - `backend-core` (.NET 8 All-in-One phục vụ API + SignalR + Frontend PWA + Uploads)
   - `caddy` (Reverse Proxy tự cấp & gia hạn SSL HTTPS Let's Encrypt + WebSockets SignalR)
   - `autoheal` (Tự động hồi phục container nếu phát hiện unhealthy)

Sau khi hoàn tất, truy cập ngay: **`https://<DOMAIN_NAME>`**!

---

## 2. Quy trình Cập nhật 1 Lệnh (`update-vps.sh`) & Tự động Đồng bộ App (OTA Auto-Sync)

Mỗi khi bạn sửa giao diện hoặc bổ sung tính năng mới trên máy cá nhân và đẩy lên GitHub:

```bash
# Trên VPS Ubuntu, chỉ cần chạy duy nhất 1 lệnh:
cd /opt/smartstudy
sudo ./deploy/vps/update-vps.sh
```

### Cơ chế Tự động Đồng bộ (OTA Auto-Sync Engine) hoạt động ra sao?
1. Container `backend-core` sau khi build lại sẽ tự tính mã **`buildVersion`** mới duy nhất (dựa trên timestamp Assembly + hash thư mục `/frontend`) và trả về tại `GET /api/system/version` kèm header `Cache-Control: no-store, no-cache, must-revalidate`.
2. Trên mọi Laptop và Điện thoại đã cài App PWA, dịch vụ `otaUpdater.service.js` tự kiểm tra `GET /api/system/version?_t=...` khi mở App, khi chuyển tab quay lại (`visibilitychange`) và định kỳ mỗi 60 giây.
3. Ngay khi phát hiện mã `version` trên máy chủ khác với `smartstudy_app_version` đang lưu trên thiết bị:
   - Hiện thanh thông báo mượt mà góc trên màn hình: *"Phát hiện phiên bản mới — Đang tự động đồng bộ giao diện..."*
   - Gửi lệnh `FORCE_SYNC_UPDATE` tới Service Worker (`sw.js`) để xóa sạch toàn bộ Cache cũ.
   - Lưu mã `version` mới và tự động reload mượt mà sau **1.2 giây** — **100% thiết bị đồng bộ ngay lập tức mà người dùng không cần xóa cache hay gỡ cài lại App!**

---

## 3. Hướng dẫn Chuyển App trên Laptop & Điện thoại từ `localhost` sang Tên miền VPS 24/7

> **Tại sao cần cài lại App 1 lần duy nhất khi đổi từ `localhost` sang VPS?**
> Vì App PWA gắn chặt với **Origin (tên miền)** lúc bạn bấm Cài đặt. App cài từ `http://localhost:5000` sẽ luôn gọi về máy tính cá nhân của bạn; còn App cài từ `https://smartstudy.duckdns.org` sẽ kết nối thẳng lên VPS chạy 24/7 — **tắt laptop đi điện thoại và các máy khác vẫn học nhóm bình thường!**

### Trên Laptop (Windows / macOS — Chrome hoặc Edge)
1. **Gỡ bản cũ `localhost`:** Mở App SmartStudy cũ $\rightarrow$ bấm biểu tượng **3 dấu chấm (`⋮`)** góc trên phải cửa sổ App $\rightarrow$ chọn **Gỡ cài đặt Smart Study Workspace (Uninstall)**.
2. **Cài bản VPS 24/7:** Mở trình duyệt Chrome/Edge $\rightarrow$ truy cập **`https://<TEN-MIEN-VPS-CUA-BAN>`** $\rightarrow$ bấm nút **"Tải App"** trên thanh điều hướng (hoặc biểu tượng cài đặt trên thanh địa chỉ) $\rightarrow$ **Install**.
3. Từ giờ trở đi, biểu tượng Smart Study trên Desktop/Taskbar sẽ chạy 24/7 từ VPS và tự cập nhật mỗi khi bạn chạy `update-vps.sh`.

### Trên Điện thoại (Android & iPhone)
- **Android (Chrome):** Xóa biểu tượng cũ nếu trước đó cài từ link tạm $\rightarrow$ mở Chrome vào **`https://<TEN-MIEN-VPS>`** $\rightarrow$ nhấn **"Tải App"** hoặc menu `⋮` $\rightarrow$ **Cài đặt ứng dụng**.
- **iPhone / iPad (Safari):** Mở Safari vào **`https://<TEN-MIEN-VPS>`** $\rightarrow$ bấm nút **Chia sẻ (Share)** $\rightarrow$ **Thêm vào MH chính (Add to Home Screen)**.

---

## 4. Phương án Thay thế: Chạy Ngầm trên Máy tính Windows (Không cần mở `run.bat`)

Nếu chưa thuê VPS mà muốn máy tính cá nhân chạy ngầm tự động mỗi khi bật nguồn Windows:

```powershell
# Mở PowerShell (khuyên dùng Run as Administrator) tại thư mục gốc dự án:
powershell -ExecutionPolicy Bypass -File scripts\background-service\install-windows-service.ps1
```

Nếu bạn có **Cloudflare Named Tunnel Token** (tạo miễn phí tại Cloudflare Zero Trust Dashboard để giữ **1 tên miền HTTPS cố định vĩnh viễn**):
```powershell
powershell -ExecutionPolicy Bypass -File scripts\background-service\install-windows-service.ps1 -CloudflareTunnelToken "PASTE_YOUR_TOKEN_HERE"
```

- **Kiểm tra trạng thái dịch vụ bất cứ lúc nào:** Nhấp đúp `scripts\background-service\service-status.bat`.
- **Gỡ bỏ hoàn toàn khi không cần chạy ngầm nữa:** Chạy `scripts\background-service\uninstall-windows-service.ps1`.
