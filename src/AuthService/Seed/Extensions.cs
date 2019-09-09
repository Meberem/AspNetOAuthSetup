using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AuthService.Models;
using IdentityModel;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Mappers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AuthService.Seed
{
    public static class Extensions
    {
        public static async Task SeedData(this IWebHost host)
        {
            using (var seedScope = host.Services.CreateScope())
            using (var scope = seedScope.ServiceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var options = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;

                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                await userManager.SeedAdminUsers(options);

                var configurationDbContext = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
                await configurationDbContext.SeedConfig(options);
            }
        }

        public static async Task SeedAdminUsers(this UserManager<AppUser> userManager, SeedOptions options)
        {
            var adminClaim = new Claim(JwtClaimTypes.Role, options.AdminRole);
            foreach (var seededUser in options.AdminUsers)
            {
                var existingUser = await userManager.FindByNameAsync(seededUser.Email);
                if (existingUser != null)
                {
                    continue;
                }

                var appUser = new AppUser
                {
                    UserName = seededUser.Email,
                    Email = seededUser.Email,
                    EmailConfirmed = true,
                };
                var createResult = await userManager.CreateAsync(appUser, seededUser.Password);
                if (!createResult.Succeeded)
                {
                    throw new Exception(createResult.Errors.First().Description);
                }

                await userManager.AddClaimAsync(appUser, adminClaim);
            }
        }

        public static async Task SeedConfig(this ConfigurationDbContext context, SeedOptions options)
        {
            foreach (var client in options.Clients)
            {
                var exists = await context.Clients.AnyAsync(c => c.ClientId == client.ClientId);
                if (exists)
                {
                    continue;
                }

                context.Clients.Add(client.ToEntity());
                await context.SaveChangesAsync();
            }

            foreach (var resource in options.IdentityResources)
            {
                var exists = await context.IdentityResources.AnyAsync(r => r.Name == resource.Name);
                if (exists)
                {
                    continue;
                }

                context.IdentityResources.Add(resource.ToEntity());
                await context.SaveChangesAsync();
            }

            foreach (var resource in options.ApiResources)
            {
                var exists = await context.ApiResources.AnyAsync(a => a.Name == resource.Name);
                if (exists)
                {
                    continue;
                }

                context.ApiResources.Add(resource.ToEntity());
                await context.SaveChangesAsync();
            }
        }
    }
}