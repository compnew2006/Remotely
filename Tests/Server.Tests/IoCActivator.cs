using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Data;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using System;
using System.Collections.Generic;

namespace Remotely.Server.Tests;

[TestClass]
public class IoCActivator
{
    public static IServiceProvider ServiceProvider => _webApp?.Services ?? 
        throw new InvalidOperationException("The web application has not been initialized.");
    private static WebApplicationBuilder? _builder;
    private static WebApplication? _webApp;

    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
    {
        _builder = WebApplication.CreateBuilder();

        var appSettings = new Dictionary<string, string?>
        {
            ["ApplicationOptions:DbProvider"] = "inmemory"
        };
        _builder.Configuration.AddInMemoryCollection(appSettings);

        _builder.Services.AddDbContext<AppDb, TestingDbContext>(
            contextLifetime: ServiceLifetime.Transient,
            optionsLifetime: ServiceLifetime.Transient);

        _builder.Services.
            AddIdentity<RemotelyUser, IdentityRole>(options => options.Stores.MaxLengthForKeys = 128)
                .AddEntityFrameworkStores<AppDb>()
                .AddDefaultTokenProviders();

        _builder.Services.AddTransient<IAppDbFactory, AppDbFactory>();
        _builder.Services.AddSingleton<ISettingsService, SettingsService>();
        _builder.Services.AddSingleton<IDeviceService, DeviceService>();
        _builder.Services.AddSingleton<IApiTokenService, ApiTokenService>();
        _builder.Services.AddSingleton<IAlertService, AlertService>();
        _builder.Services.AddSingleton<IOrganizationService, OrganizationService>();
        _builder.Services.AddSingleton<IUserService, UserService>();
        _builder.Services.AddSingleton<IInviteService, InviteService>();
        _builder.Services.AddSingleton<IDeviceGroupService, DeviceGroupService>();
        _builder.Services.AddSingleton<ISavedScriptService, SavedScriptService>();
        _builder.Services.AddSingleton<IScriptScheduleService, ScriptScheduleService>();
        _builder.Services.AddSingleton<IScriptResultService, ScriptResultService>();
        _builder.Services.AddSingleton<ISharedFileService, SharedFileService>();
        _builder.Services.AddSingleton<IDeviceListingService, DeviceListingService>();
        _builder.Services.AddSingleton<IDeviceGroupMembershipService, DeviceGroupMembershipService>();
        _builder.Services.AddSingleton<IMaintenanceService, MaintenanceService>();
        _builder.Services.AddTransient<IEmailSenderEx, EmailSenderEx>();

        _webApp = _builder.Build();
    }
}

