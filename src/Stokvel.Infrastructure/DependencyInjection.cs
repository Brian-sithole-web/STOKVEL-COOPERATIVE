using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stokvel.Application.Common;
using Stokvel.Application.Services;
using Stokvel.Domain;
using Stokvel.Infrastructure.Email;
using Stokvel.Infrastructure.Identity;
using Stokvel.Infrastructure.Persistence;
using Stokvel.Infrastructure.Security;
using Stokvel.Infrastructure.Services;

namespace Stokvel.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddStokvelInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<StokvelDbContext>(options => options.UseSqlite(connectionString));
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<StokvelDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromHours(2));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IContributionService, ContributionService>();
        services.AddScoped<ILedgerService, LedgerService>();
        services.AddScoped<ILoanService, LoanService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISettingsService, SettingsService>();
        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StokvelDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var role in new[] { SystemRoles.PlatformAdmin, SystemRoles.User })
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new ApplicationRole(role));
        }

        if (!await db.PlatformSettings.AnyAsync())
            db.PlatformSettings.Add(new Domain.Entities.PlatformSettings());

        await db.SaveChangesAsync();
    }
}
