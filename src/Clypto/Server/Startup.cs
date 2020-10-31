using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clypto.Server.Data;
using Clypto.Server.Services;
using DSharpPlus;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using System.Reflection;
using AutoMapper;

namespace Clypto.Server
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var discordConfig = new DiscordConfiguration
            {
                Token = Configuration["DiscordBot:Token"],
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Debug
            };

            services.AddAutoMapper(Assembly.GetExecutingAssembly());
            services.AddTransient<IClipRepository, ClipMongoRepository>();
            services.AddTransient<AudioLoudnessNormalizer>();
            services.AddTransient<CloudBlobClient>(s =>
                CloudStorageAccount.Parse(Configuration.GetConnectionString("AzureStorage")).CreateCloudBlobClient());
            services.AddTransient<CloudBlobContainerAccessor>();
            services.AddSingleton<AzureBlobService>();
            services.AddTransient<DataSeeder>();
            services.AddSingleton<DiscordClient>(s => new DiscordClient(discordConfig));
            services.AddSingleton<DiscordIntegrationService>();
            services.AddSingleton<DiscordVoiceService>();
            services.AddControllersWithViews();
            services.AddRazorPages();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseWebAssemblyDebugging();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseBlazorFrameworkFiles();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
                endpoints.MapFallbackToFile("index.html");
            });
        }
    }
}
