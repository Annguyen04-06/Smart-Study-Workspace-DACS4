/*
 * Điểm khởi chạy SmartStudy.API — All-in-One Host trên cổng 5000 và 5500
 * (daemon-watchdog chạy: --urls "http://0.0.0.0:5000;http://0.0.0.0:5500"):
 * - REST API (/api/...), SignalR Hub (/hubs/study-room), Swagger (/swagger)
 * - File tĩnh đã tải lên (wwwroot/uploads: PDF, Avatar)
 * - Toàn bộ giao diện /frontend (HTML/JS/CSS/PWA) phục vụ cùng origin
 * - Nén Brotli/Gzip cho HTML/JS/CSS/JSON (bỏ qua /hubs để không ảnh hưởng SignalR)
 * => Triệt tiêu lỗi CORS và lỗi HTTPS Mixed Content khi truy cập qua Cloudflare Tunnel.
 */
using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using SmartStudy.API.Hubs;
using SmartStudy.API.Middlewares;
using SmartStudy.API.Services;
using SmartStudy.Application;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Application.Common.Models;
using SmartStudy.Infrastructure;
using SmartStudy.Infrastructure.Persistence;
using SmartStudy.Infrastructure.Persistence.Seed;

// Đảm bảo thư mục wwwroot/uploads tồn tại trước khi khởi tạo WebApplication
var initialWebRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
Directory.CreateDirectory(Path.Combine(initialWebRoot, "uploads", "materials"));
Directory.CreateDirectory(Path.Combine(initialWebRoot, "uploads", "avatars"));

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "wwwroot"
});

// Nạp cấu hình bí mật cục bộ (như GoogleSmtp AppPassword) có hỗ trợ Hot-Reload và không đẩy lên Git
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Nhận diện đúng scheme HTTPS + IP thật của client khi đi qua Cloudflare Tunnel / Reverse Proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

const string UniversalCorsPolicy = "SmartStudyUniversalCors";
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

// CORS động: chấp nhận Origin khai báo trong appsettings, Localhost, dải IP LAN, Radmin VPN (26.x),
// mọi tên miền *.trycloudflare.com (Cloudflare Quick Tunnel) và *.ngrok-free.app
builder.Services.AddCors(options =>
{
    options.AddPolicy(UniversalCorsPolicy, policy =>
    {
        policy
            .SetIsOriginAllowed(origin => IsTrustedOrigin(origin, configuredOrigins))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<PresenceTracker>();
builder.Services.AddSingleton<RoomPomodoroTracker>();
builder.Services.AddSingleton<RoomWhiteboardTracker>();
builder.Services.AddScoped<IStudyRoomRealtimeNotifier, SignalRRealtimeNotifier>();

// Xác định sớm thư mục /frontend để chia sẻ chung cho StaticFileProvider và AppVersionProvider (OTA Auto-Sync)
var resolvedFrontendRoot = ResolveFrontendDirectory(
    builder.Configuration["FrontendHosting:RootPath"],
    builder.Environment.ContentRootPath,
    AppContext.BaseDirectory);
builder.Services.AddSingleton(new AppVersionProvider(resolvedFrontendRoot));

// Link mở App trên điện thoại (Cloudflare Tunnel HTTPS do daemon-watchdog ghi + IP mạng LAN)
builder.Services.Configure<MobileAccessOptions>(builder.Configuration.GetSection(MobileAccessOptions.SectionName));
builder.Services.AddSingleton<MobileAccessProvider>();

// Nén phản hồi Brotli (ưu tiên) + Gzip: giảm mạnh dung lượng HTML/JS/CSS/JSON trên mạng 4G/Wi-Fi
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
    [
        "application/manifest+json",
        "image/svg+xml",
        "text/javascript"
    ]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

var effectiveWebRoot = builder.Environment.WebRootPath ?? initialWebRoot;
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration, effectiveWebRoot);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Chuẩn hóa lỗi Model Binding sang đúng cấu trúc ApiResponse camelCase
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Dữ liệu đầu vào không hợp lệ." : e.ErrorMessage)
                .Distinct()
                .ToList();

            var response = ApiResponse<object?>.Fail(errors.FirstOrDefault() ?? "Dữ liệu không hợp lệ.", errors);
            return new BadRequestObjectResult(response);
        };
    });

builder.Services
    .AddSignalR(hubOptions =>
    {
        hubOptions.EnableDetailedErrors = builder.Environment.IsDevelopment();
        hubOptions.MaximumReceiveMessageSize = 64 * 1024;
    })
    .AddJsonProtocol(protocol =>
    {
        protocol.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Smart Study Workspace Core API",
        Version = "v1",
        Description = "ASP.NET Core 8 Clean Architecture REST API & SignalR Realtime Hub"
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Nhập chuỗi JWT theo cú pháp: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

var app = builder.Build();

// Khởi chạy tự động tạo bảng và nạp dữ liệu mẫu khi kết nối SQL Server sẵn sàng
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbBootstrap");
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await DbSeeder.SeedAsync(dbContext, passwordHasher, effectiveWebRoot, logger);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Chưa thể khởi tạo tự động SQL Server lúc khởi động. Kiểm tra chuỗi kết nối DefaultConnection trong appsettings.json.");
    }
}

// ForwardedHeaders phải chạy đầu tiên để mọi middleware phía sau thấy đúng scheme https của Cloudflare
app.UseForwardedHeaders();

// Nén mọi phản hồi trừ kênh SignalR /hubs (WebSocket/SSE cần đẩy dữ liệu tức thì, không qua bộ đệm nén)
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/hubs"),
    branch => branch.UseResponseCompression());

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Smart Study Workspace API v1");
});

app.UseCors(UniversalCorsPolicy);

var contentTypeProvider = CreateStaticContentTypeProvider();

// Đảm bảo thư mục uploads tồn tại tại WebRoot thực tế (kể cả khi ContentRoot khác thư mục chạy lệnh)
Directory.CreateDirectory(Path.Combine(effectiveWebRoot, "uploads", "materials"));
Directory.CreateDirectory(Path.Combine(effectiveWebRoot, "uploads", "avatars"));

// Phục vụ wwwroot (PDF, Avatar) — cho phép nhúng xem trực tiếp PDF trong thẻ <iframe> của Frontend
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider,
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        if (ctx.File.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers["Content-Disposition"] = "inline";
        }
    }
});

// Phục vụ trực tiếp toàn bộ giao diện /frontend trên cùng cổng 5000 (All-in-One Host)
var frontendPath = resolvedFrontendRoot;

if (frontendPath is not null)
{
    var frontendProvider = new PhysicalFileProvider(frontendPath);

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = frontendProvider,
        RequestPath = "",
        DefaultFileNames = new List<string> { "index.html" }
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = frontendProvider,
        RequestPath = "",
        ContentTypeProvider = contentTypeProvider,
        OnPrepareResponse = ctx => ApplyFrontendCacheHeaders(ctx.Context.Response, ctx.File.Name)
    });

    app.Logger.LogInformation("Đã gắn giao diện Frontend tại: {FrontendPath}", frontendPath);
}
else
{
    app.Logger.LogWarning("Không tìm thấy thư mục /frontend (chứa index.html). Backend chỉ phục vụ API + SignalR.");
}

app.UseAuthentication();
app.UseAuthorization();

// Endpoint kiểm tra sức khỏe hệ thống cho script Local VPS và Docker healthcheck
app.MapGet("/api/health", () => Results.Ok(ApiResponse<object>.Ok(new
{
    service = "SmartStudy.API",
    status = "Healthy",
    serverTime = DateTime.UtcNow,
    frontendMounted = frontendPath is not null
}, "Backend Core đang hoạt động ổn định.")));

app.MapControllers();
app.MapHub<StudyRoomHub>("/hubs/study-room");

app.Run();

// Kiểm tra Origin có thuộc nhóm tin cậy (cấu hình tĩnh, mạng nội bộ hoặc tên miền đường hầm HTTPS)
static bool IsTrustedOrigin(string? origin, IReadOnlyCollection<string> configuredOrigins)
{
    if (string.IsNullOrWhiteSpace(origin)) return true;
    if (configuredOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)) return true;
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;

    var host = uri.Host.ToLowerInvariant();
    return host == "localhost"
        || host == "127.0.0.1"
        || host.StartsWith("192.168.")
        || host.StartsWith("10.")
        || host.StartsWith("172.")
        || host.StartsWith("26.")
        || host.EndsWith(".trycloudflare.com")
        || host.EndsWith(".ngrok-free.app")
        || host.EndsWith(".duckdns.org")
        || host.EndsWith(".nip.io")
        || uri.Scheme == Uri.UriSchemeHttps;
}

// Ánh xạ MIME chuẩn cho ES6 Module, PWA Manifest, SVG và PDF
static FileExtensionContentTypeProvider CreateStaticContentTypeProvider()
{
    var provider = new FileExtensionContentTypeProvider();
    provider.Mappings[".webmanifest"] = "application/manifest+json";
    provider.Mappings[".js"] = "application/javascript; charset=utf-8";
    provider.Mappings[".mjs"] = "application/javascript; charset=utf-8";
    provider.Mappings[".css"] = "text/css; charset=utf-8";
    provider.Mappings[".html"] = "text/html; charset=utf-8";
    provider.Mappings[".svg"] = "image/svg+xml";
    provider.Mappings[".pdf"] = "application/pdf";
    return provider;
}

// Dò tìm thư mục /frontend: ưu tiên cấu hình FrontendHosting:RootPath, sau đó đi ngược lên các thư mục cha
static string? ResolveFrontendDirectory(string? configuredPath, params string[] startPaths)
{
    if (!string.IsNullOrWhiteSpace(configuredPath))
    {
        var fullConfiguredPath = Path.GetFullPath(configuredPath, startPaths[0]);
        if (File.Exists(Path.Combine(fullConfiguredPath, "index.html")))
        {
            return fullConfiguredPath;
        }
    }

    foreach (var startPath in startPaths)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "frontend");
            if (File.Exists(Path.Combine(candidate, "index.html")))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
    }

    return null;
}

// Bắt buộc chống cache cứng cho HTML, JS, CSS, sw.js, manifest & version.json để OTA Auto-Sync đồng bộ tức thì
static void ApplyFrontendCacheHeaders(HttpResponse response, string fileName)
{
    var lowerName = fileName.ToLowerInvariant();
    if (lowerName == "sw.js"
        || lowerName == "version.json"
        || lowerName.EndsWith(".webmanifest")
        || lowerName.EndsWith(".html")
        || lowerName.EndsWith(".js")
        || lowerName.EndsWith(".mjs")
        || lowerName.EndsWith(".css"))
    {
        response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        response.Headers.Pragma = "no-cache";
        response.Headers.Expires = "0";
    }
}
