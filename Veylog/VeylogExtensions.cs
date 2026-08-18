using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Veylog.Interceptors;
using Veylog.Middleware;
using Veylog.Pages;

namespace Veylog;

public static class VeylogExtensions
{
    public static IServiceCollection AddVeylog(
        this IServiceCollection services,
        Action<VeylogOptions> configure)
    {
        var options = new VeylogOptions();

        configure(options);

        services.AddSingleton(options);

        services.AddDbContext<LogDbContext>(db =>
        {
            db.UseSqlServer(options.ConnectionString);
        });

        services.AddSingleton<SqlLoggingInterceptor>();

        services.AddScoped<LibraryAccessFilter>();

        services.AddRazorPages(options =>
        {
            options.Conventions.AddFolderApplicationModelConvention(
                "/veylog",
                model =>
                {
                    model.Filters.Add(
                        new TypeFilterAttribute(typeof(LibraryAccessFilter)));
                });
        })
        .AddApplicationPart(typeof(IndexModel).Assembly);

        services
            .AddAuthentication()
            .AddCookie("VeylogScheme", options =>
            {
                options.Cookie.Name = "Veylog.Auth";
                options.LoginPath = "/veylog/login";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
            });

        services.AddSingleton<VeylogTokenManager>();

        return services;
    }
    public static DbContextOptionsBuilder AddVeylogSqlInterceptor(
        this DbContextOptionsBuilder options,
        IServiceProvider serviceProvider)
    {
        options.AddInterceptors(
            serviceProvider.GetRequiredService<SqlLoggingInterceptor>());

        return options;
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