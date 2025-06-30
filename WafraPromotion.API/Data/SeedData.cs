using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration; // Required for IConfiguration
using Microsoft.Extensions.Logging; // Required for ILogger

namespace WafraPromotion.API.Data
{
    public static class SeedData
    {
        public static async Task InitializeAdminUser(IServiceProvider serviceProvider, IConfiguration configuration, ILogger logger)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            string adminRole = configuration["AdminCredentials:RoleName"] ?? "Admin";
            string adminEmail = configuration["AdminCredentials:Username"] ?? "admin@example.com";
            string adminPassword = configuration["AdminCredentials:Password"] ?? "AdminPass123!"; // Default if not in config

            // Ensure admin role exists
            if (await roleManager.FindByNameAsync(adminRole) == null)
            {
                await roleManager.CreateAsync(new IdentityRole(adminRole));
                logger.LogInformation($"Admin role '{adminRole}' created.");
            }

            // Ensure admin user exists
            if (await userManager.FindByNameAsync(adminEmail) == null)
            {
                var adminUser = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true // Confirm email since we are creating it
                };

                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, adminRole);
                    logger.LogInformation($"Admin user '{adminEmail}' created and assigned to role '{adminRole}'.");
                }
                else
                {
                    logger.LogError($"Error creating admin user '{adminEmail}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
            else
            {
                logger.LogInformation($"Admin user '{adminEmail}' already exists.");
            }
        }
    }
}
