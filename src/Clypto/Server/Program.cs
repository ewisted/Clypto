using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using Clypto.Server.Data;
using Clypto.Server.Services;
using System.IO;

namespace Clypto.Server
{
	public class Program
	{
		public static async Task Main(string[] args)
		{
			var host = CreateHostBuilder(args).Build();
			var services = host.Services;

			// Add configuration sources
			var config = services.GetRequiredService<IConfiguration>();

			var envName = config.GetValue<string>("ASPNETCORE_ENVIRONMENT");
			Console.WriteLine($"Environment: {envName}");

			// Compatibility fix for running on linux
			AppContext.SetSwitch("System.Net.Http.UseSocketsHttpHandler", false);

			// Configure logging
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Debug()
				.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
				.MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Error)
				.MinimumLevel.Override("HealthChecks.UI.Core.HostedService.HealthCheckReportCollector", LogEventLevel.Error)
				.MinimumLevel.Override("HealthChecks.UI.Core.HostedService.HealthCheckCollectorHostedService", LogEventLevel.Error)
				.MinimumLevel.Override("System.Net.Http.HttpClient.health-checks", LogEventLevel.Error)
				.MinimumLevel.Override("Microsoft.EntityFrameworkCore.Infrastructure", LogEventLevel.Error)
				.Enrich.FromLogContext()
				.WriteTo.Console()
				//.WriteTo.ApplicationInsightsEvents(config["APPINSIGHTS_INSTRUMENTATIONKEY"])
				//.WriteTo.DiscordWebhook(ulong.Parse(config.GetValue<string>("DiscordWebhookLogger:WebhookId")), config.GetValue<string>("DiscordWebhookLogger:WebhookSecret"))
				.CreateLogger();

			await ProcessSwitches(args, config, services);

			using (host)
			{
				await host.Services.GetRequiredService<DiscordIntegrationService>().InitializeAsync(services);
				// Start webhost
				await host.RunAsync();
			}
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.ConfigureWebHostDefaults(webBuilder =>
				{
					webBuilder.UseStartup<Startup>();
				});

		public static bool ConfigContains(string key, string[] args, IConfiguration config)
		{
			return args.Contains($"/{key}") ||
					args.Contains($"--{key}") ||
					args.Contains($"-{key}") ||
					config.GetValue<bool>(key);
		}

		public static async Task ProcessSwitches(string[] args, IConfiguration config, IServiceProvider services)
		{
			var env = services.GetRequiredService<IWebHostEnvironment>();

			var seedDataPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "SeedData.json");
			var genSeed = ConfigContains("generate-seed", args, config) || env.IsDevelopment();
			var normOn = ConfigContains("normalize-existing", args, config);
			var uploadToAzure = ConfigContains("upload-clips", args, config);
			var downloadClipsFromArgs = ConfigContains("download-clips", args, config);
			var downloadClips = downloadClipsFromArgs || false; // TODO read env to check if it should seed
			var seedDbFromArgs = args.Contains("/seed-db");
			var seedDb = seedDbFromArgs; // TODO replace True when Env is Dev
			var dropDb = env.IsDevelopment() || env.IsStaging();
			dropDb = false;
			seedDb = true;

			// Generated updated seed file and exit
			if (genSeed)
			{
				await GenerateSeed(services, config, seedDataPath, true);
				if (!env.IsDevelopment())
				{
					Environment.Exit(0);
				}
			}

			// Normalize loudness and exit
			if (normOn)
			{
				await NormalizeExistingClips(services);
				if (!uploadToAzure)
					Environment.Exit(0);
			}

			// Upload clips to azure and exit
			if (uploadToAzure)
			{
				await UploadClipsToAzure(services);
				Environment.Exit(0);
			}

			// Download all clips if not already downloaded
			if (downloadClips)
			{
				// TODO download these on demand instead
				var clipDownloader = services.GetRequiredService<AzureBlobService>();
				await clipDownloader.DownloadAllClipsAsync();
				if (downloadClipsFromArgs)
					Environment.Exit(0);
			}

			// Drop Database
			if (dropDb)
			{
				var repo = services.GetRequiredService<IClipRepository>();
				await repo.DropDbAsync();
			}

			// Seed the database
			if (seedDb)
			{
				var dbDataSeeder = services.GetRequiredService<DataSeeder>();
				await dbDataSeeder.SeedDataAsync(seedDataPath);
				if (seedDbFromArgs)
					Environment.Exit(0);
			}
		}

		public static async Task GenerateSeed(IServiceProvider services, IConfiguration config, string seedDataPath, bool useProd = false)
		{
			SeedDataGenerator seedDataGenerator;
			if (useProd)
			{
				seedDataGenerator = new SeedDataGenerator(
					new ClipMongoRepository(
						config["SeedDb:ConnectionString"],
						config["SeedDb:DbName"]));
			}
			else
			{
				seedDataGenerator = services.GetRequiredService<SeedDataGenerator>();
			}


			await seedDataGenerator.CreateSeedDataFile(seedDataPath);
		}

		public static async Task NormalizeExistingClips(IServiceProvider services)
		{
			var loudnessNormalizer = services.GetRequiredService<AudioLoudnessNormalizer>();
			var logger = services.GetRequiredService<ILogger<AudioLoudnessNormalizer>>();

			var files = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "clips"))
				.Where(f => f.EndsWith(".mp3") && !f.EndsWith(".original"));
			var results = new Dictionary<NormalizerResult, int>()
			{
				{NormalizerResult.Error, 0},
				{NormalizerResult.Skipped, 0},
				{NormalizerResult.Updated, 0}
			};
			foreach (var file in files)
			{
				var result = await loudnessNormalizer.NormalizeFileLoudness(file);
				results[result]++;
			}
			logger.LogInformation("Normalization complete. Skipped: {skipped} Updated: {updated} Errored: {errored}",
				results[NormalizerResult.Skipped],
				results[NormalizerResult.Updated],
				results[NormalizerResult.Error]);
		}

		public static async Task UploadClipsToAzure(IServiceProvider services)
		{
			var loggerFactory = services.GetRequiredService<ILoggerFactory>();
			var logger = loggerFactory.CreateLogger("AzureUploader");

			logger.LogInformation("Beginning upload of clips to azure");

			var files = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "clips"))
				.Where(f => f.EndsWith(".mp3") && !f.EndsWith(".original"));

			int uploadedCount = 0;
			int skippedCount = 0;
			foreach (var file in files)
			{
				var blobContainerAccessor = services.GetRequiredService<CloudBlobContainerAccessor>();
				var azureContainer = await blobContainerAccessor.GetContainerAsync();

				var blob = azureContainer.GetBlockBlobReference(Path.GetFileName(file));
				await blob.FetchAttributesAsync();
				if (blob.Properties.Length != new FileInfo(file).Length)
				{
					logger.LogInformation("{clip} Uploading", file);
					await blob.UploadFromFileAsync(file);
					logger.LogInformation("{clip} Upload Complete", file);
					uploadedCount++;
				}
				else
				{
					skippedCount++;
				}
			}
			logger.LogInformation("Completed upload of clips to azure. Uploaded: {uploaded} Skipped: {skipped}", uploadedCount, skippedCount);
		}
	}
}
