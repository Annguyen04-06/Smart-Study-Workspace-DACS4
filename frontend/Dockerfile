# =============================================================================
# Smart Study Workspace — Frontend Nginx Static Host & Reverse Proxy
# =============================================================================
FROM nginx:alpine

WORKDIR /usr/share/nginx/html

# Thay thế cấu hình mặc định bằng Nginx Reverse Proxy
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Sao chép toàn bộ mã nguồn Frontend (HTML, CSS, ES6 JS, Icons, Manifest, SW)
COPY . .

EXPOSE 80

# Healthcheck kiểm tra Nginx sẵn sàng phục vụ
HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
    CMD wget -qO- http://localhost/healthz || exit 1

CMD ["nginx", "-g", "daemon off;"]
