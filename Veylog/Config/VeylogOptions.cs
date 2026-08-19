using Microsoft.AspNetCore.Http;
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

        /// <summary>
        /// Optional callback invoked for each API request that recorded an
        /// exception, once its log entry has been saved to the database.
        /// Receives the request path and the saved log's database Id.
        /// Runs from the background flush service, not the request pipeline,
        /// so it never affects request latency or the original exception flow.
        /// </summary>
        public Func<string, long, Task>? OnError { get; set; }
    }
}
