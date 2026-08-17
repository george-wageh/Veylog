using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Veylog.Interceptors;
using Veylog.Middleware;
using Veylog.Pages;

namespace Veylog;

public static class OcuLogExtensions
{
    public static IServiceCollection AddVeylog(
        this IServiceCollection services,
        Action<OcuLogOptions> configure)
    {
        var options = new OcuLogOptions();

        configure(options);

        services.AddSingleton(options);

        services.AddDbContext<LogDbContext>(db =>
        {
            db.UseSqlServer(options.ConnectionString);
        });

        services.AddSingleton<SqlLoggingInterceptor>();

        services.AddRazorPages()
            .AddApplicationPart(typeof(IndexModel).Assembly);

        return services;
    }

    public static WebApplication UseVeylog(
        this WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<LogDbContext>();

            db.Database.Migrate();
        }

        app.UseMiddleware<ApiLoggingMiddleware>();

        app.MapRazorPages();

        return app;
    }
}