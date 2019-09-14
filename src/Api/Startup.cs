using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Logging;
using NSwag;
using NSwag.AspNetCore;
using NSwag.Generation.Processors.Security;

namespace Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            IdentityModelEventSource.ShowPII = true;
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.Audience = Configuration["Authentication:Audience"];
                    options.Authority = Configuration["Authentication:Authority"];
                });

            services.AddOpenApiDocument(options =>
            {
                options.AddSecurity(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
                {
                    Type = OpenApiSecuritySchemeType.OAuth2,
                    ExtensionData = new Dictionary<string, object>
                    {
                        ["x-tokenName"] = "id_token"
                    },
                    Description = "Authenticate using our Auth Service",
                    AuthorizationUrl = $"{Configuration["Authentication:Authority"]}/connect/authorize",
                    Scopes = new Dictionary<string, string>
                    {
                        [$"{Configuration["Authentication:Scope"]}"] = "The Scope we want the token to have"
                    }
                });

                options.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor(JwtBearerDefaults.AuthenticationScheme));
            });

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseOpenApi()
                .UseSwaggerUi3(settings =>
                {
                    settings.OAuth2Client = new OAuth2ClientSettings
                    {
                        ClientId = Configuration["Authentication:ClientId"],
                    };
                });

            app.UseAuthentication();
            app.UseHttpsRedirection();
            app.UseMvc();
        }
    }
}
