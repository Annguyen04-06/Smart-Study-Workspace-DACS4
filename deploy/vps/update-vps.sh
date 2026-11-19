#!/usr/bin/env bash
# =============================================================================
# Smart Study Workspace — Cập nhật 1 Lệnh trên VPS (Kích hoạt OTA Auto-Sync)
# Chỉ cần đẩy code mới lên GitHub rồi chạy lệnh này trên VPS:
#   sudo ./deploy/vps/update-vps.sh
# Ngay sau khi container lên sóng, mọi App PWA đã cài trên Laptop và Điện thoại
# sẽ tự phát hiện buildVersion mới qua /api/system/version và tự đồng bộ giao diện!
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
COMPOSE_FILE="${SCRIPT_DIR}/docker-compose.prod.yml"
ENV_FILE="${SCRIPT_DIR}/.env.production"

echo "============================================================================="
echo "   SMART STUDY WORKSPACE — CẬP NHẬT NHANH VPS & KÍCH HOẠT OTA AUTO-SYNC      "
echo "============================================================================="

if [[ ! -f "${ENV_FILE}" ]]; then
    echo "[ERROR] Không tìm thấy ${ENV_FILE}. Vui lòng chạy deploy-vps.sh trước."
    exit 1
fi

cd "${PROJECT_ROOT}"

echo "[1/4] Kéo mã nguồn mới nhất từ Git..."
if [[ -d ".git" ]]; then
    git pull --ff-only
else
    echo "      [INFO] Thư mục hiện tại không phải Git repo — tiến hành build trực tiếp từ mã nguồn trên đĩa."
fi

echo "[2/4] Build lại các service ứng dụng (backend-core, ai-service)..."
docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" build backend-core ai-service

echo "[3/4] Khởi chạy bản cập nhật mới (giữ nguyên SQL Server & toàn bộ dữ liệu)..."
docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" up -d --no-deps backend-core ai-service
docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" up -d caddy autoheal

echo "[4/4] Dọn dẹp image rác và kiểm tra mã phiên bản OTA mới..."
docker image prune -f >/dev/null 2>&1 || true

for i in $(seq 1 25); do
    if docker exec smartstudy-prod-backend curl -fsS http://localhost:5000/api/health >/dev/null 2>&1; then
        break
    fi
    sleep 2
done

NEW_VER=$(docker exec smartstudy-prod-backend curl -fsS http://localhost:5000/api/system/version 2>/dev/null || echo '{"message":"starting"}')
echo ""
echo "============================================================================="
echo "   [HOÀN TẤT] ĐÃ CẬP NHẬT VPS THÀNH CÔNG!"
echo "   Phiên bản mới: ${NEW_VER}"
echo "   -> Toàn bộ App trên Laptop & Điện thoại sẽ tự động đồng bộ trong <= 60 giây."
echo "============================================================================="
