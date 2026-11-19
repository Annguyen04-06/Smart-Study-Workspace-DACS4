using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Infrastructure.ExternalServices;
using SmartStudy.Infrastructure.Identity;
using SmartStudy.Infrastructure.Persistence;
using SmartStudy.Infrastructure.Storage;

namespace SmartStudy.Infrastructure;

/// <summary>
/// Đăng ký toàn bộ dịch vụ tầng Infrastructure: EF Core SQL Server, JWT Identity (kèm SignalR query token),
/// Typed HttpClient sang Python FastAPI và Local File Storage.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string webRootPath)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Chưa cấu hình chuỗi kết nối 'DefaultConnection' trong appsettings.json.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
            }));

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        // Cấu hình JWT Authentication đồng bộ cho cả HTTP Header và SignalR WebSockets (/hubs/study-room)
        var jwtSection = configuration.GetSection(JwtSettings.SectionName);
        services.Configure<JwtSettings>(jwtSection);
        var jwtSettings = jwtSection.Get<JwtSettings>() ?? new JwtSettings();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                // Quy tắc SignalR bắt buộc: Trích xuất JWT từ query string access_token cho kết nối /hubs/study-room
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/hubs/study-room", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        // Đăng ký dịch vụ gửi Email OTP khôi phục mật khẩu qua Google Gmail SMTP
        services.Configure<GoogleSmtpSettings>(configuration.GetSection(GoogleSmtpSettings.SectionName));
        services.AddScoped<IEmailSender, GoogleSmtpEmailSender>();

        // Đăng ký Typed HttpClient giao tiếp sang Python FastAPI Microservice
        services.Configure<AiMicroserviceSettings>(configuration.GetSection(AiMicroserviceSettings.SectionName));
        // Tái sử dụng kết nối TCP keep-alive tới FastAPI (bỏ bắt tay lại mỗi câu hỏi @bot), làm mới định kỳ để cập nhật DNS
        services.AddHttpClient<IAiMicroserviceClient, PythonAiMicroserviceClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 32
            })
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

        // Đăng ký lưu trữ tệp cục bộ tại wwwroot/uploads
        services.AddSingleton(new FileStorageOptions { WebRootPath = webRootPath });
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();

        return services;
    }
}
