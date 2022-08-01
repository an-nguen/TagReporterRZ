using System;
using System.Globalization;
using BlazorDownloadFile;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using Radzen;
using TagReporter.Datasource;
using TagReporter.Domains;
using TagReporter.Security;
using TagReporter.Services;
using TagReporter.Settings;

namespace TagReporter;

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
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        var settings = new TagReporterDatabaseSettings();
        Configuration.GetSection(nameof(TagReporterDatabaseSettings)).Bind(settings);

        services.Configure<TagReporterDatabaseSettings>(
            Configuration.GetSection(nameof(TagReporterDatabaseSettings)));
        services.Configure<EmailSettings>(Configuration.GetSection(nameof(EmailSettings)));
        services.Configure<SmbSettings>(Configuration.GetSection(nameof(SmbSettings)));

        services.AddDbContext<TagReporterContext>(options =>
            options.UseSqlServer(Configuration.GetConnectionString("TagReporterDatabase")));
        services.AddDatabaseDeveloperPageExceptionFilter();

        services.AddHttpClient();
        services.AddControllers();
        services.AddTransient<WiredTagMeasurementService>();
        services.AddTransient<ZoneService>();
        services.AddTransient<TagService>();
        services.AddTransient<AccountService>();
        services.AddTransient<ReportService>();
        services.AddSingleton<EmailService>();
        // Radzen
        services.AddScoped<DialogService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<TooltipService>();
        services.AddScoped<ContextMenuService>();

        services.Configure<CookiePolicyOptions>(options =>
        {
            options.CheckConsentNeeded = _ => true;
            options.MinimumSameSitePolicy = SameSiteMode.None;
        });

        services.AddIdentity<ApplicationUser, ApplicationRole>()
            .AddEntityFrameworkStores<TagReporterContext>()
            .AddDefaultTokenProviders();

        services.Configure<IdentityOptions>(opt =>
        {
            opt.Password.RequireDigit = false;
            opt.Password.RequireLowercase = false;
            opt.Password.RequireUppercase = false;
            opt.Password.RequireNonAlphanumeric = false;
            opt.Password.RequiredLength = 8;
            opt.Password.RequiredUniqueChars = 1;
        });

        services.AddScoped(serviceProvider =>
        {
            var logger = serviceProvider.GetService<ILogger<UserService>>();
            var userManager = serviceProvider.GetService<UserManager<ApplicationUser>>();
            var signInManager = serviceProvider.GetService<SignInManager<ApplicationUser>>();
            if (userManager == null || logger == null || signInManager == null)
                throw new NullReferenceException(
                    "userManager/roleManager/logger/signInManager cannot be null (AddScoped)");

            return new UserService(userManager, logger, signInManager);
        });
        services.AddScoped(serviceProvider =>
        {
            var roleManager = serviceProvider.GetService<RoleManager<ApplicationRole>>();
            if (roleManager == null)
                throw new NullReferenceException(
                    "userManager/roleManager/logger/signInManager cannot be null (AddScoped)");
            return new RoleService(roleManager);
        });

        services.AddScoped<IHostEnvironmentAuthenticationStateProvider>(sp =>
            (ServerAuthenticationStateProvider)sp.GetRequiredService<AuthenticationStateProvider>()
        );
        services.AddHangfire((container, configuration) =>
        {
            configuration
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(
                    container.GetService<IConfiguration>().GetConnectionString("TagReporterDatabase"),
                    new SqlServerStorageOptions
                    {
                        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                        QueuePollInterval = TimeSpan.Zero,
                        UseRecommendedIsolationLevel = true,
                        DisableGlobalLocks = true // Migration to Schema 7 is required
                    });
        });

        services.AddLogging();
        services.AddHangfireServer();
        services.AddBlazorDownloadFile();
        services.AddRazorPages();
        services.AddServerSideBlazor();

        services.AddAuthentication().AddYandex(options =>
        {
            options.ClientId = "9f2af364c7de429e8d22e8879bcd44d2";
            options.ClientSecret = "ac2ace253fd246f588e54924a70e4183";
        });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        var cultureInfo = new CultureInfo("ru-RU");

        CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
        CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new [] { new MyHangfireAuthFilter() }
        });


        app.UseEndpoints(endpoints =>
        {
            endpoints.MapBlazorHub();
            endpoints.MapControllers();
            endpoints.MapDefaultControllerRoute();
            endpoints.MapFallbackToPage("/_Host");
            endpoints.MapHangfireDashboard();
        });
    }
}