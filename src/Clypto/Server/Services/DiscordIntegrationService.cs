using DSharpPlus;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serilog;
using DSharpPlus.CommandsNext;
using Microsoft.Extensions.Configuration;
using Clypto.Server.Commands;

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
                StringPrefixes = stringPrefixes,
                EnableDms = true,
                EnableMentionPrefix = true,
                Services = provider
            };

            _client.MessageCreated += (client, msg) =>
            {
                if (!msg.Message.Content.ToLower().StartsWith("pp "))
                {
                    return Task.CompletedTask;
                }

                int argsPos = 3;
                var clipArgs = msg.Message.Content.Substring(argsPos);



                var command = _commands.FindCommand("play", out string rawArguments);
                var context = _commands.CreateFakeContext(msg.Author, msg.Channel, $"play {clipArgs}", "play", command, clipArgs);
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
