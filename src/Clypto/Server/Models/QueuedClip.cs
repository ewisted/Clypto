using Clypto.Server.Data.Models;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Clypto.Server.Models
{
    public class QueuedClip
    {
        public DiscordChannel VoiceChannel { get; set; }
        public Clip Clip { get; set; }
        public string Path { get; set; }

        public QueuedClip(DiscordChannel voiceChannel, Clip clip, string clipPath)
        {
            VoiceChannel = voiceChannel;
            Clip = clip;
            Path = clipPath;
        }
    }
}
