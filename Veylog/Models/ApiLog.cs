using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Veylog.Models
{
    public class ApiLog
    {
        public long Id { get; set; }

        public string TraceId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public string HttpMethod { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string? QueryString { get; set; }

        public string? UserId { get; set; }

        public string? IpAddress { get; set; }

        public int StatusCode { get; set; }

        public long ElapsedMilliseconds { get; set; }

        public string? RequestBody { get; set; }

        public string? ResponseBody { get; set; }

        public string? Exception { get; set; }
    }
}
