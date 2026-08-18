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
        public bool EnableRequestLogging { get; set; } = false;
        public bool EnableResponseLogging { get; set; } = false;
        public bool EnableSqlParametersLogging { get; set; } = false;

        public bool EnableSqlLogging { get; set; } = true;

        public int FlushIntervalSeconds { get; set; } = 30;
        public int MaxBatchSize { get; set; } = 200;
    }
}
