using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Veylog.Models
{
    public class SqlLog
    {
        public long Id { get; set; }

        public string? TraceId { get; set; }

        public DateTime CreatedAt { get; set; }

        public string CommandText { get; set; } = string.Empty;

        public string? Parameters { get; set; }

        public long ElapsedMilliseconds { get; set; }

        public bool IsSuccess { get; set; }

        public string? Exception { get; set; }

        public SqlOperation SqlOperation { get; set; } = SqlOperation.None;
    }
    public enum SqlOperation
    {
        None = 0,
        Reader=1,
        NonQuery,
        Scalar
    }
}
