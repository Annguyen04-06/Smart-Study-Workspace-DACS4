# =============================================================================
# Smart Study Workspace — Backend Core .NET 8 Multi-Stage Dockerfile
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy Solution và các file .csproj để cache layer restore
COPY SmartStudy.sln ./
COPY src/SmartStudy.Domain/SmartStudy.Domain.csproj src/SmartStudy.Domain/
COPY src/SmartStudy.Application/SmartStudy.Application.csproj src/SmartStudy.Application/
COPY src/SmartStudy.Infrastructure/SmartStudy.Infrastructure.csproj src/SmartStudy.Infrastructure/
COPY src/SmartStudy.API/SmartStudy.API.csproj src/SmartStudy.API/
COPY tests/SmartStudy.UnitTests/SmartStudy.UnitTests.csproj tests/SmartStudy.UnitTests/

RUN dotnet restore

# Copy toàn bộ mã nguồn và biên dịch Release
COPY src/ ./src/
RUN dotnet publish src/SmartStudy.API/SmartStudy.API.csproj -c Release -o /app/publish /p:UseAppHost=false

# =============================================================================
# Runtime Stage
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Cài đặt curl phục vụ healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Tạo cấu trúc thư mục uploads lưu trữ file PDF và avatar
RUN mkdir -p /app/wwwroot/uploads/materials /app/wwwroot/uploads/avatars

ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000

VOLUME ["/app/wwwroot/uploads"]

# Healthcheck kiểm tra Backend sẵn sàng phục vụ
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:5000/api/study-rooms/public || exit 1

ENTRYPOINT ["dotnet", "SmartStudy.API.dll"]
