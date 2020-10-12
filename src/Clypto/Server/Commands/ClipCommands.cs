using Clypto.Server.Data;
using Clypto.Server.Data.Models;
using Clypto.Server.Services;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Clypto.Server.Commands
{
    public class ClipCommands : BaseCommandModule
    {
        private readonly DiscordVoiceService _voiceSvc;
        private readonly IClipRepository _clipRepo;
        private readonly AzureBlobService _blobService;

        public ClipCommands(DiscordVoiceService voiceSvc, IClipRepository clipRepo, AzureBlobService blobService)
        {
            _voiceSvc = voiceSvc;
            _clipRepo = clipRepo;
            _blobService = blobService;
        }

        [Command("play"), Description("Plays the specified clip.")]
        public async Task PlayClipAsync(CommandContext ctx, [RemainingText, Description("The name of the clip to play.")] string clipName)
        {
            if (ctx.Channel.IsPrivate)
            {
                await ctx.RespondAsync("This command is not supported in private messages");
                return;
            }

            var voiceChannel = ctx.Member.VoiceState.Channel;
            if (voiceChannel == null)
            {
                await ctx.RespondAsync("You must be joined to a voice channel to play a clip.");
                return;
            }

            var dbClip = _clipRepo.GetByCommand(clipName);
            if (dbClip == null)
            {
                await ctx.RespondAsync($"The requested clip \"{clipName}\" was not found.");
                return;
            }

            await _blobService.EnsureClipDownloadedAsync(dbClip);

            string clipFullPath = Path.Combine(Directory.GetCurrentDirectory(), "clips", dbClip.FileName);
            var result = await _voiceSvc.QueueClipForPlaybackAsync(ctx.Guild, voiceChannel, dbClip, clipFullPath);
            if (result)
            {
                dbClip.Counter++;
                await _clipRepo.Update(dbClip.Id, dbClip);
            }
            else
            {
                await ctx.RespondAsync("Unable to queue clip, the queue may be full. Please try again later.");
            }
        }

        [Command("list"), Description("Lists the available clips.")]
        public async Task ListClipsAsync(CommandContext ctx, [Description("Number of clips to list. Default is all (-1).")] int numOfClips = -1)
        {
            IEnumerable<Clip> clipsInDb = numOfClips == -1 ? _clipRepo.Get() : _clipRepo.Get(0, numOfClips);

            foreach (var embed in await EmbedFactory.GetListClipEmbeds(clipsInDb))
            {
                await ctx.RespondAsync("", false, embed);
            }
        }

        [Command("tags"), Description("List all the unique tags for the all the clips")]
        public async Task ListTagsAsync(CommandContext ctx)
        {
            var tags = string.Join(", ", _clipRepo.GetAllTags());
            await ctx.RespondAsync($"Tags: {tags}");
        }

        [Command("add-tag"), Description("List all the unique tags for the all the clips")]
        public async Task AddTag(CommandContext ctx, [Description("Name of the clip to add tags to.")] string clipName, [Description("Tags to add to the clip.")] params string[] inTags)
        {
            var clip = _clipRepo.GetByCommand(clipName);
            if (clip == null)
            {
                await ctx.RespondAsync($"The requested clip \"{clipName}\" was not found.");
                return;
            }

            if (inTags != null && inTags.Any())
            {
                IEnumerable<string> duplicateTags = inTags.Intersect(clip.Tags);

                if (duplicateTags != null && duplicateTags.Any())
                {
                    // remove the dup clips from the inTags list
                    foreach (var dupTags in duplicateTags.ToList())
                    {
                        inTags = inTags.Where(x => x != dupTags).ToArray();
                    }

                }

                var tags = inTags;

                if (tags == null || !tags.Any())
                {
                    await ctx.RespondAsync("All specified tags already exist. Please enter new tags if needed.");
                    return;
                }
                tags = tags.Union(clip.Tags).ToArray();
                clip.Tags = tags;
            }
            else
            {
                await ctx.RespondAsync("BadArgCount: The input text has too few parameters.");
                return;
            }
            var result = await _clipRepo.Update(clip.Id, clip);
            Log.Information("Clip Updated {0}", result.ModifiedCount);

            if (result.ModifiedCount > 0)
            {
                await ctx.RespondAsync("Record successful updated");
            }
            else
            {
                await ctx.RespondAsync("Record failed to update");
            }

        }
    }
}
