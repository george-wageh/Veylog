using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Veylog.Interceptors;
using Veylog.Middleware;

namespace Veylog
{
    public static class OcuLogExtensions
    {
        public static IServiceCollection AddOcuLog(
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

            return services;
        }

        public static IApplicationBuilder UseVeylog(
            this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<LogDbContext>();

            db.Database.Migrate();

            app.UseMiddleware<ApiLoggingMiddleware>();

            return app;
        }
    }
}
