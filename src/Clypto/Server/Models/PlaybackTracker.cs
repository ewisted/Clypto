using Clypto.Server.Data.Models;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Clypto.Server.Models
{
    public class PlaybackTracker
    {
        public DiscordGuild Guild { get; }
        public GuildSettings Settings { get; set; }

        private bool isInactive;

        public bool IsInactive
        {
            get { return isInactive; }
            set 
            {
                if (!isInactive && value)
                {
                    inactiveSince = DateTime.UtcNow;
                    Task.Run(async () => await StartInactivityTimer());
                }
                else if (isInactive && !value)
                {
                    inactiveSince = null;
                    tokenSource.Cancel();
                }
                isInactive = value;
            }
        }
        public TimeSpan TimeInactive
        {
            get
            {
                if (isInactive && inactiveSince.HasValue)
                {
                    return DateTime.UtcNow - inactiveSince.Value;
                }
                else
                {
                    return TimeSpan.FromSeconds(0);
                }
            }
        }
        private DateTime? inactiveSince { get; set; }
        private Queue<QueuedClip> queue { get; }
        private CancellationTokenSource tokenSource { get; set; }

        public event EventHandler InactivityThresholdReached;

        public PlaybackTracker(DiscordGuild guild, GuildSettings settings)
        {
            Guild = guild;
            Settings = settings;
            queue = new Queue<QueuedClip>(settings.QueueSize);
            tokenSource = new CancellationTokenSource();
            isInactive = true;
        }

        public bool QueueClip(DiscordChannel voiceChannel, Clip clip, string clipPath)
        {
            if (queue.Count >= Settings.QueueSize)
            {
                return false;
            }
            else
            {
                if (isInactive) IsInactive = false;
                queue.Enqueue(new QueuedClip(voiceChannel, clip, clipPath));
                return true;
            }
        }

        public bool TryDequeueClip(out QueuedClip clip)
        {
            if (queue.Count > 0)
            {
                if (isInactive) IsInactive = false;
                clip = queue.Dequeue();
                return true;
            }
            else
            {
                IsInactive = true;
                clip = null;
                return false;
            }
        }

        private async Task StartInactivityTimer()
        {
            if (Settings.InactivityBehavior == InactivityBehavior.Timeout)
            {
                try
                {
                    await Task.Delay(Settings.InactiveTimeout, tokenSource.Token);
                    InactivityThresholdReached.Invoke(this, new EventArgs());
                }
                catch (OperationCanceledException)
                {
                    tokenSource = new CancellationTokenSource();
                }
            }
        }
    }
}
