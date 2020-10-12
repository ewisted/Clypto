using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Clypto.Server.Data.Models
{
    public class GuildSettings
    {
        public ulong GuildId { get; set; }
        public InactivityBehavior InactivityBehavior { get; set; }
        public TimeSpan InactiveTimeout { get; set; }
        public int QueueSize { get; set; }

        public GuildSettings()
        {
            InactivityBehavior = InactivityBehavior.Disconnect;
            InactiveTimeout = TimeSpan.FromSeconds(0);
            QueueSize = 3;
        }
    }

    public enum InactivityBehavior
    {
        Disconnect,
        Timeout,
        Manual
    }
}
