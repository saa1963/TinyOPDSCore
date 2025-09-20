using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using TinyOPDSCore.Misc;

namespace TinyOPDSCore
{
    public class Startup
    {
        private ILogger<Startup> logger;
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            Properties.config = configuration;
            Localizer.Init();
            Localizer.SetLanguage(Properties.Language);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var loggerFactory = new NLogLoggerFactory();
            logger = loggerFactory.CreateLogger<Startup>();
            logger.LogInformation("Startup исполнен.");
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                    builder =>
                    {
                        builder.AllowAnyOrigin()
                                            .AllowAnyHeader()
                                            .AllowAnyMethod();
                    });
            });
            services.AddSingleton(typeof(Watcher2));
        }

        //private void Fsw_Created(object sender, FileSystemEventArgs e)
        //{
        //    //ZipQueues.Enqueue(new(e.Name, e.FullPath));
        //    
        //    logger.LogInformation("Watcher2 - " + e.Name + "; " + e.FullPath);
        //}

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            app.UseResponseRewind();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            //app.UseHttpsRedirection();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseRouting();

            app.UseCors();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
