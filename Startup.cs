using IdentityServer.Data;
using IdentityServer.Models;
using IdentityServer.Quickstart;
using IdentityServer4;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Mappers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Reflection;

namespace IdentityServer
{
    public class Startup
    {
        public IHostingEnvironment Environment { get; }

        public Startup(IHostingEnvironment environment)
        {
            Environment = environment;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(Microsoft.AspNetCore.Mvc.CompatibilityVersion.Version_2_1);

            const string connectionString = @"Data Source=your database source;database=your db name;trusted_connection=yes;";

            var migrationsAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly))
                );

            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
            services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>,
                UserClaimsPrincipal>();

            var builder = services.AddIdentityServer()

                // this adds the config data from DB (clients, resources)
                .AddConfigurationStore(options =>
                {
                    options.ConfigureDbContext = b =>
                        b.UseSqlServer(connectionString,
                            sql => sql.MigrationsAssembly(migrationsAssembly));
                })
                // this adds the operational data from DB (codes, tokens, consents)
                .AddOperationalStore(options =>
                {
                    options.ConfigureDbContext = b =>
                        b.UseSqlServer(connectionString,
                            sql => sql.MigrationsAssembly(migrationsAssembly));

                    // this enables automatic token cleanup. this is optional.
                    options.EnableTokenCleanup = true;
                })
                .AddAspNetIdentity<ApplicationUser>();

            services.AddAuthentication()

             .AddGoogle("Google", options =>
             {
                 options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;

                 options.ClientId = "you client id";
                 options.ClientSecret = "your client secret";
             });
            builder.AddDeveloperSigningCredential();
        }

        public void Configure(IApplicationBuilder app)
        {
            InitializeDatabase(app);
            app.UseDeveloperExceptionPage();
            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseIdentityServer();
            app.UseMvcWithDefaultRoute();
        }

        private void InitializeDatabase(IApplicationBuilder app)
        {
            using (var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
            {
                SeedData.EnsureSeedData(serviceScope);
            }
        }

        public class SeedData
        {
            public static void EnsureSeedData(IServiceScope serviceScope)
            {
                serviceScope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.Migrate();

                var context = serviceScope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
                context.Database.Migrate();
                EnsureSeedData(context);
            }

            private static void EnsureSeedData(ConfigurationDbContext context)
            {
                if (!context.Clients.Any())
                {
                    foreach (var client in Config.GetClients().ToList())
                    {
                        context.Clients.Add(client.ToEntity());
                    }
                    context.SaveChanges();
                }
                else
                {
                    foreach (var client in Config.GetClients().ToList())
                    {
                        var item = context.Clients
                            .Include(x => x.RedirectUris)
                            .Include(x => x.PostLogoutRedirectUris)
                            .Include(x => x.ClientSecrets)
                            .Include(x => x.Claims)
                            .Include(x => x.AllowedScopes)
                            .Include(x => x.AllowedCorsOrigins)
                            .Include(x => x.AllowedGrantTypes)
                            .Where(c => c.ClientId == client.ClientId).FirstOrDefault();
                        if (item != null)
                        {
                            context.Clients.Remove(item);
                        }
                        context.Clients.Add(client.ToEntity());
                    }
                    context.SaveChanges();
                }

                if (!context.IdentityResources.Any())
                {
                    foreach (var resource in Config.GetIdentityResources().ToList())
                    {
                        context.IdentityResources.Add(resource.ToEntity());
                    }
                    context.SaveChanges();
                }
                else
                {
                    foreach (var resource in Config.GetIdentityResources().ToList())
                    {
                        var item = context.IdentityResources.Where(c => c.Name == resource.Name).FirstOrDefault();
                        if (item != null)
                        {
                            context.IdentityResources.Remove(item);
                        }
                        context.IdentityResources.Add(resource.ToEntity());
                    }
                    context.SaveChanges();
                }
            }
        }
    }
}