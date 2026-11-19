using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SmartStudy.Application.Services;

namespace SmartStudy.Application;

/// <summary>
/// Đăng ký toàn bộ Validators và Business Services của tầng Application vào Dependency Injection Container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IMaterialService, MaterialService>();
        services.AddScoped<IQuizService, QuizService>();
        services.AddScoped<IFlashcardService, FlashcardService>();
        services.AddScoped<IAdminService, AdminService>();

        return services;
    }
}
