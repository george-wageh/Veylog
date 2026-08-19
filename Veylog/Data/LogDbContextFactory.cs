using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Veylog
{
    public class LogDbContextFactory
        : IDesignTimeDbContextFactory<LogDbContext>
    {
        public LogDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder =
                new DbContextOptionsBuilder<LogDbContext>();

            optionsBuilder.UseSqlServer(
                "Server=.;Database=testveylog;Trusted_Connection=True;TrustServerCertificate=True");

            return new LogDbContext(optionsBuilder.Options);
        }
    }
}
