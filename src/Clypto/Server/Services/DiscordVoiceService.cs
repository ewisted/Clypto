using Clypto.Shared;
using Clypto.Server.Data.Models;
using Clypto.Server.Models;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DSharpPlus.EventArgs;
using System.Linq;
using System.Collections.Generic;

namespace Clypto.Server.Services
{
    public class DiscordVoiceService
    {
        private readonly DiscordClient _client;
        private readonly VoiceNextExtension _voice;

        private static readonly ConcurrentDictionary<ulong, PlaybackTracker> _connections = new ConcurrentDictionary<ulong, PlaybackTracker>();

        public DiscordVoiceService(DiscordClient client)
        {
            _client = client;
            _voice = _client.UseVoiceNext();
            _client.VoiceStateUpdated += HandleForcedBotDisconnect;
        }

        public async Task<VoiceNextConnection> JoinOrChangeVoiceAsync(DiscordGuild guild, DiscordChannel voiceChannel)
        {
            VoiceNextConnection connection;
            if (TryGetConnection(guild, out connection))
            {
                if (connection.Channel.Name == voiceChannel.Name)
                {
                    return connection;
                }
                Log.Warning("Tried to join {newchannel} but was already joined to {oldchannel} in {guild}. Disconnecting from old channel and joining new one.", voiceChannel.Name, connection.Channel.Name, guild.Name);
                connection.Disconnect();
            }

            connection = await _voice.ConnectAsync(voiceChannel);
            // _connections.TryAdd(voiceChannel.Id, connection);
            Log.Information("Successfully joined voice to {channel} in {guild}.", voiceChannel.Name, guild.Name);
            return connection;
        }

        public Task LeaveVoiceAsync(DiscordGuild guild)
        {
            VoiceNextConnection connection;
            if (TryGetConnection(guild, out connection))
            {
                var voiceChannelName = connection.Channel.Name;
                connection.Disconnect();
                Log.Information("Successfully disconnected voice from {channel} in {guild}.", voiceChannelName, guild.Name);
                return Task.CompletedTask;
            }
            else
            {
                Log.Warning("Tried to leave a voice channel but wasn't joined to one in {guild}.", guild.Name);
                return Task.CompletedTask;
            }
        }

        public async Task<bool> QueueClipForPlaybackAsync(DiscordGuild guild, DiscordChannel voiceChannel, Clip clip, string clipPath)
        {
            if (!_connections.TryGetValue(guild.Id, out PlaybackTracker tracker))
            {
                var settings = new GuildSettings();
                tracker = new PlaybackTracker(guild, settings);
                tracker.InactivityThresholdReached += HandleInactivityTimeout;
            }

            var isInactive = tracker.IsInactive ? true : false;

            var result = tracker.QueueClip(voiceChannel, clip, clipPath);
            if (!result) return false;

            _connections[guild.Id] = tracker;
            if (isInactive) await Task.Run(async () => await StreamClipsAsync(guild));
            return true;
        }

        private bool TryGetConnection(DiscordGuild guild, out VoiceNextConnection connection)
        {
            connection = _voice.GetConnection(guild);
            if (connection != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private async Task StreamClipsAsync(DiscordGuild guild)
        {
            try
            {
                if (!_connections.TryGetValue(guild.Id, out PlaybackTracker tracker))
                {
                    throw new ArgumentNullException("Playback tracker was not found for {guild}.", guild.Name);
                }

                if (!tracker.TryDequeueClip(out QueuedClip clip))
                {
                    if (tracker.Settings.InactivityBehavior == InactivityBehavior.Disconnect)
                    {
                        await LeaveVoiceAsync(guild);
                    }
                    return;
                }

                var connection = await JoinOrChangeVoiceAsync(guild, clip.VoiceChannel);
                if (connection == null) throw new Exception("Unable to establish a voice channel connection.");

                using (var ffmpeg = CreateProcess(clip.Path))
                {
                    using (var stream = connection.GetTransmitStream())
                    {
                        try
                        {
                            await connection.SendSpeakingAsync();
                            await ffmpeg.StandardOutput.BaseStream.CopyToAsync(stream);
                            await connection.WaitForPlaybackFinishAsync();
                        }
                        finally
                        {
                            await stream.FlushAsync();
                            await connection.SendSpeakingAsync(false);
                        }
                    }
                }

                await StreamClipsAsync(guild);
            }
            catch (Exception ex)
            {
                Log.Error("Error occurred when streaming attempting to stream a clip in {guild}: {error}", guild.Name, ex.Message);
            }
        }

        private async void HandleInactivityTimeout(object sender, EventArgs e)
        {
            PlaybackTracker tracker = (PlaybackTracker)sender;
            tracker.InactivityThresholdReached -= HandleInactivityTimeout;
            Log.Information("Inactivity timeout reached for {guildname}. Leaving voice.", tracker.Guild.Name);
            await LeaveVoiceAsync(tracker.Guild);
        }

        private Task HandleForcedBotDisconnect(object sender, VoiceStateUpdateEventArgs e)
        {
            if (e.User.Id != _client.CurrentUser.Id || e.After.Channel != null) return Task.CompletedTask;

            var tracker = _connections[e.Guild.Id];
            if (tracker == null) return Task.CompletedTask;

            tracker.IsInactive = true;
            _connections[e.Guild.Id] = tracker;
            return Task.CompletedTask;
        }

        private static Process CreateProcess(string path)
        {
            var ffmpegPath = PathUtilities.GetFullPath("ffmpeg.exe");
            if (!File.Exists(ffmpegPath))
            {
                ffmpegPath = PathUtilities.GetFullPath("ffmpeg");
            }
            if (!File.Exists(ffmpegPath))
            {
                throw new ArgumentNullException("ffmpeg not found");
            }

            return Process.Start(new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
        }
    }
}
