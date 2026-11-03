using FluentValidation;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.DTOs.Chat;
using GymRatService.Common.DTOs.Sessions;
using GymRatService.Common.DTOs.Users;
using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Filtering;
using GymRatService.Repository;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Security;
using GymRatService.Service.Services;
using GymRatService.Service.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GymRatService.Service;

public static class ServiceLayerExtensions
{
    public static IServiceCollection AddServiceLayer(this IServiceCollection services, string? connectionString)
    {
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        services.AddRepositoryLayer(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddSingleton<IValidator<UpdateUserSettingsRequest>, UpdateUserSettingsRequestValidator>();
        services.AddSingleton<IValidator<ExerciseFilter>, ExerciseFilterValidator>();
        services.AddSingleton<IValidator<WorkoutFilter>, WorkoutFilterValidator>();
        services.AddSingleton<IValidator<CreateWorkoutRequest>, CreateWorkoutRequestValidator>();
        services.AddSingleton<IValidator<RenameWorkoutRequest>, RenameWorkoutRequestValidator>();
        services.AddSingleton<IValidator<ReplaceWorkoutExercisesRequest>, ReplaceWorkoutExercisesRequestValidator>();
        services.AddSingleton<IValidator<int>, SplitDayValidator>();
        services.AddSingleton<IValidator<StartSessionRequest>, StartSessionRequestValidator>();
        services.AddSingleton<IValidator<SaveExerciseSetsRequest>, SaveExerciseSetsRequestValidator>();
        services.AddSingleton<IValidator<ChatMessageRequest>, ChatMessageRequestValidator>();
        services.AddSingleton<IChatUsageLimiter, ChatUsageLimiter>();

        services.AddScoped<IExercisesService, ExercisesService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUsersService, UsersService>();
        services.AddScoped<IWorkoutsService, WorkoutsService>();
        services.AddScoped<ISplitService, SplitService>();
        services.AddScoped<ISessionsService, SessionsService>();
        services.AddScoped<IChatService, ChatService>();

        return services;
    }
}
