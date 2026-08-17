using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Veylog
{
    public class VeylogOptions
    {
        public string ConnectionString { get; set; } = string.Empty;

        public bool EnableApiLogging { get; set; } = true;

        public bool EnableSqlLogging { get; set; } = true;

        public long SlowQueryThresholdMs { get; set; } = 500;
    }
}
