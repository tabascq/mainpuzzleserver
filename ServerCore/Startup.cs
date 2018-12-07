﻿using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServerCore.Areas.Deployment;
using ServerCore.DataModel;

namespace ServerCore
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public Startup(IHostingEnvironment env)
        {
            // Set up to use Azure settings
            IConfigurationBuilder configBuilder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = configBuilder.Build();
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc()
                .AddRazorPagesOptions(options =>
                {
                    options.Conventions.AuthorizeFolder("/Events");
                    options.Conventions.AuthorizeFolder("/Puzzles");
                    options.Conventions.AuthorizeFolder("/Shared");
                    options.Conventions.AuthorizeFolder("/Teams");
                });

            DeploymentConfiguration.ConfigureDatabase(Configuration, services);

            services.AddAuthentication().AddMicrosoftAccount(microsoftOptions =>
            {
                microsoftOptions.ClientId = Configuration["Authentication-Microsoft-ApplicationId"];
                microsoftOptions.ClientSecret = Configuration["Authentication-Microsoft-Password"];

            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            PuzzleServerContext.UpdateDatabase(app);

            if (env.IsDevelopment() || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                app.UseBrowserLink();
                app.UseDeveloperExceptionPage();
            }
            else
            {

                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            // According to the Identity Scaffolding readme the order of the following calls matters
            // Must be UseStaticFiles, UseAuthentication, UseMvc
            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseMvc();
        }
    }
}
