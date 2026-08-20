using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Veylog.Models;

namespace Veylog
{
    public class LogDbContext : DbContext
    {
        public LogDbContext(DbContextOptions<LogDbContext> options)
            : base(options)
        {
        }

        public DbSet<ApiLog> ApiLogs => Set<ApiLog>();

        public DbSet<SqlLog> SqlLogs => Set<SqlLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("Veylog");

            modelBuilder.Entity<ApiLog>()
                .HasIndex(x => x.Path);

            modelBuilder.Entity<ApiLog>()
                .HasIndex(x => x.TraceId);

            modelBuilder.Entity<ApiLog>()
                .HasIndex(x => x.CreatedAt);

            modelBuilder.Entity<ApiLog>()
                .HasIndex(x => new { x.Path, x.CreatedAt });

            modelBuilder.Entity<SqlLog>()
                .HasIndex(x => x.TraceId);

            modelBuilder.Entity<SqlLog>()
                .HasIndex(x => x.CreatedAt);
        }
    }
    
}
