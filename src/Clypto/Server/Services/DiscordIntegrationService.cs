using DSharpPlus;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serilog;
using DSharpPlus.CommandsNext;
using Microsoft.Extensions.Configuration;
using Clypto.Server.Commands;
using System.Linq;
using DSharpPlus.Entities;

namespace Clypto.Server.Services
{
    public class DiscordIntegrationService
    {
        private readonly DiscordClient _client;
        private readonly IConfiguration _config;
        private CommandsNextExtension _commands;

        public DiscordIntegrationService(DiscordClient client, IConfiguration config)
        {
            // DI
            _client = client;
            _config = config;
        }

        public async Task InitializeAsync(IServiceProvider provider)
        {
            // Create command next config
            var stringPrefixes = new List<string>();
            _config.GetSection("Discord:StringPrefixes").Bind(stringPrefixes);
            var commandConfig = new CommandsNextConfiguration
            {
                EnableDms = true,
                EnableMentionPrefix = true,
                Services = provider
            };

            _client.MessageCreated += (client, msg) =>
            {
                if (!stringPrefixes.Any(s => msg.Message.Content.ToLower().StartsWith($"{s} ")))
                {
                    return Task.CompletedTask;
                }

                int argsPos = 3;
                var clipArgs = msg.Message.Content.Substring(argsPos);

                var commandParts = clipArgs.Split(' ');
                var commandPrefix = commandParts.FirstOrDefault();
                if (commandPrefix == null)
                {
                    var mentions = new List<IMention>();
                    mentions.Add(new UserMention(msg.Author));
                    Task.Run(async () => await msg.Channel.SendMessageAsync($"Could not execute command or clip: could not find a match for input \"{clipArgs}\"", false, null, mentions));
                    return Task.CompletedTask;
                }

                CommandContext context;
                var command = _commands.FindCommand(commandPrefix, out string rawArguments);
                if (command != null)
                {
                    context = _commands.CreateFakeContext(msg.Author, msg.Channel, clipArgs, commandPrefix, command, clipArgs.Substring(commandPrefix.Length).Trim());
                }
                else
                {
                    command = _commands.FindCommand("play", out rawArguments);
                    context = _commands.CreateFakeContext(msg.Author, msg.Channel, $"play {clipArgs}", "play", command, clipArgs);
                }

                Task.Run(async () => await _commands.ExecuteCommandAsync(context));
                return Task.CompletedTask;
            };

            // Register commands and voice
            _commands = _client.UseCommandsNext(commandConfig);

            // Register logging event hooks
            _client.Ready += (client, e) =>
            {
                Log.Information("Client is ready.");
                return Task.CompletedTask;
            };
            _client.GuildAvailable += (client, e) =>
            {
                Log.Information("Guild available: {guild}.", e.Guild.Name);
                return Task.CompletedTask;
            };
            _client.ClientErrored += (client, e) =>
            {
                Log.Error("Exception occured: {error}.", e.Exception.Message);
                return Task.CompletedTask;
            };
            _commands.CommandExecuted += (client, e) =>
            {
                Log.Information("{user} successfully executed '{command}'.", e.Context.User.Username, e.Command.QualifiedName);
                return Task.CompletedTask;
            };
            _commands.CommandErrored += (client, e) =>
            {
                Log.Error("{user} tried executing '{command}' and failed. \n{errortype}: {error}.", e.Context.User.Username, e.Command?.QualifiedName ?? "unknown", e.Exception?.GetType(), e.Exception?.Message ?? "no error details");
                return Task.CompletedTask;
            };

            _commands.RegisterCommands<ClipCommands>();

            // Connect to Discord
            await _client.ConnectAsync();
        }
    }
}
