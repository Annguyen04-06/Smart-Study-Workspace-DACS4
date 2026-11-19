#!/usr/bin/env bash
# =============================================================================
# Smart Study Workspace — Triển khai 1 Lệnh lên VPS Ubuntu/Debian (24/7 + HTTPS)
# Cách chạy:
#   chmod +x deploy/vps/deploy-vps.sh && sudo ./deploy/vps/deploy-vps.sh
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
COMPOSE_FILE="${SCRIPT_DIR}/docker-compose.prod.yml"
ENV_FILE="${SCRIPT_DIR}/.env.production"

echo "============================================================================="
echo "   SMART STUDY WORKSPACE — TRIỂN KHAI PRODUCTION VPS 24/7 (AUTO HTTPS)       "
echo "============================================================================="

if [[ $EUID -ne 0 ]]; then
    echo "[ERROR] Vui lòng chạy script này với quyền sudo: sudo ./deploy/vps/deploy-vps.sh"
    exit 1
fi

# 1. Tự kiểm tra RAM & tạo Swap 3GB nếu VPS RAM thấp (< 4GB) để SQL Server 2022 chạy mượt
TOTAL_RAM_MB=$(free -m | awk '/^Mem:/{print $2}')
TOTAL_SWAP_MB=$(free -m | awk '/^Swap:/{print $2}')
echo "[1/6] Kiểm tra tài nguyên máy chủ: RAM=${TOTAL_RAM_MB}MB | Swap=${TOTAL_SWAP_MB}MB"

if [[ "${TOTAL_RAM_MB}" -lt 4096 && "${TOTAL_SWAP_MB}" -lt 2048 ]]; then
    echo "      -> RAM dưới 4GB và chưa đủ Swap. Đang tự động tạo Swap 3GB (/swapfile)..."
    fallocate -l 3G /swapfile || dd if=/dev/zero of=/swapfile bs=1M count=3072
    chmod 600 /swapfile
    mkswap /swapfile
    swapon /swapfile
    if ! grep -q '/swapfile' /etc/fstab; then
        echo '/swapfile none swap sw 0 0' >> /etc/fstab
    fi
    sysctl vm.swappiness=20 || true
    echo "      [OK] Đã kích hoạt Swap 3GB thành công."
fi

# 2. Cài đặt Docker Engine & Docker Compose Plugin nếu chưa có
echo "[2/6] Kiểm tra Docker Engine & Docker Compose..."
if ! command -v docker >/dev/null 2>&1; then
    echo "      -> Đang cài đặt Docker chính thức cho Ubuntu/Debian..."
    apt-get update -y
    apt-get install -y ca-certificates curl gnupg lsb-release git
    curl -fsSL https://get.docker.com | sh
    systemctl enable docker
    systemctl start docker
    echo "      [OK] Đã cài xong Docker Engine."
else
    echo "      [OK] Docker đã sẵn sàng ($(docker --version))."
fi

# 3. Mở tường lửa UFW cho SSH (22), HTTP (80) và HTTPS (443)
echo "[3/6] Cấu hình tường lửa (cổng 22, 80, 443)..."
if command -v ufw >/dev/null 2>&1; then
    ufw allow 22/tcp >/dev/null 2>&1 || true
    ufw allow 80/tcp >/dev/null 2>&1 || true
    ufw allow 443/tcp >/dev/null 2>&1 || true
    ufw allow 443/udp >/dev/null 2>&1 || true
    echo "      [OK] Đã đảm bảo mở cổng 80/443 trên UFW."
fi

# 4. Kiểm tra file cấu hình bí mật .env.production
echo "[4/6] Kiểm tra tệp cấu hình ${ENV_FILE}..."
if [[ ! -f "${ENV_FILE}" ]]; then
    cp "${SCRIPT_DIR}/.env.production.example" "${ENV_FILE}"
    echo "[ACTION REQUIRED] Đã tạo mới tệp ${ENV_FILE} từ mẫu."
    echo "  -> Vui lòng mở tệp và điền DOMAIN_NAME, SQL_SA_PASSWORD, GEMINI_API_KEY, JWT_SECRET:"
    echo "     nano ${ENV_FILE}"
    echo "  -> Sau đó chạy lại: sudo ./deploy/vps/deploy-vps.sh"
    exit 1
fi

# 5. Kéo mã nguồn mới nhất từ Git (nếu thư mục là Git repository)
echo "[5/6] Đồng bộ mã nguồn mới nhất..."
cd "${PROJECT_ROOT}"
if [[ -d ".git" ]]; then
    git pull --ff-only || echo "      [WARN] Bỏ qua git pull (chưa gắn remote hoặc có chỉnh sửa cục bộ)."
fi

# 6. Build & khởi chạy 5 container 24/7 không downtime
echo "[6/6] Đang build và khởi chạy 5 Container Production (SQL Server + AI + Backend + Caddy + Autoheal)..."
docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" up -d --build --remove-orphans

echo ""
echo "[*] Đang kiểm tra sức khỏe Backend & mã phiên bản OTA Auto-Sync..."
for i in $(seq 1 30); do
    if docker exec smartstudy-prod-backend curl -fsS http://localhost:5000/api/health >/dev/null 2>&1; then
        break
    fi
    sleep 3
done

VERSION_JSON=$(docker exec smartstudy-prod-backend curl -fsS http://localhost:5000/api/system/version 2>/dev/null || echo '{"status":"starting"}')
DOMAIN_VAL=$(grep -E '^DOMAIN_NAME=' "${ENV_FILE}" | cut -d '=' -f2- | tr -d '\r')

echo ""
echo "============================================================================="
echo "   [THÀNH CÔNG] SMART STUDY WORKSPACE ĐÃ ONLINE 24/7 TRÊN VPS!"
echo "============================================================================="
if [[ "${DOMAIN_VAL}" == ":80" || -z "${DOMAIN_VAL}" ]]; then
    PUBLIC_IP=$(curl -fsS https://api.ipify.org 2>/dev/null || hostname -I | awk '{print $1}')
    echo "  * Địa chỉ truy cập (HTTP):  http://${PUBLIC_IP}"
else
    echo "  * Địa chỉ truy cập (HTTPS): https://${DOMAIN_VAL}"
fi
echo "  * OTA Build Version:        ${VERSION_JSON}"
echo "  * Lệnh cập nhật về sau:     sudo ./deploy/vps/update-vps.sh"
echo "============================================================================="
