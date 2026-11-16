using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Data;
using Remotely.Server.Services;
using Remotely.Shared.Dtos;
using Remotely.Shared.Entities;
using Remotely.Shared.Enums;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

[TestClass]
public class DataServiceTests
{
    private readonly string _newDeviceID = "NewDeviceName";
    private IAlertService _alertService = null!;
    private IApiTokenService _apiTokenService = null!;
    private IDeviceGroupMembershipService _deviceGroupMembershipService = null!;
    private IDeviceListingService _deviceListingService = null!;
    private IDeviceService _deviceService = null!;
    private IOrganizationService _organizationService = null!;
    private ISavedScriptService _savedScriptService = null!;
    private IScriptResultService _scriptResultService = null!;
    private IUserService _userService = null!;
    private TestData _testData = null!;

    [TestMethod]
    public async Task AddAlert()
    {
        await _alertService.AddAlert(_testData.Org1Device1.ID, _testData.Org1Id, "Test Message");

        var alerts = _alertService.GetAlerts(_testData.Org1Admin1.Id);

        Assert.AreEqual("Test Message", alerts.First().Message);
    }

    [TestMethod]
    public async Task AddOrUpdateDevice()
    {
        var storedDevice = (await _deviceService.GetDevice(_newDeviceID)).Value;

        Assert.IsNull(storedDevice);

        var newDevice = new DeviceClientDto()
        {
            ID = _newDeviceID,
            OrganizationID = _testData.Org1Id,
            DeviceName = Environment.MachineName,
            Is64Bit = Environment.Is64BitOperatingSystem
        };

        var result = await _deviceService.AddOrUpdateDevice(newDevice);
        Assert.IsTrue(result.IsSuccess);

        storedDevice = (await _deviceService.GetDevice(_newDeviceID)).Value;

        Assert.AreEqual(_newDeviceID, storedDevice!.ID);
        Assert.AreEqual(Environment.MachineName, storedDevice.DeviceName);
        Assert.AreEqual(Environment.Is64BitOperatingSystem, storedDevice.Is64Bit);
    }

    [TestMethod]
    public async Task CreateDevice()
    {
        var deviceOptions = new DeviceSetupOptions()
        {
            DeviceID = Guid.NewGuid().ToString(),
            DeviceAlias = "Spare Laptop",
            OrganizationID = _testData.Org1Id
        };

        // First call should create and return device.
        var savedDevice = (await _deviceService.CreateDevice(deviceOptions)).Value!;
        Assert.IsInstanceOfType(savedDevice, typeof(Device));

        // Second call with same DeviceUuid should fail.
        var secondSave = await _deviceService.CreateDevice(deviceOptions);
        Assert.IsFalse(secondSave.IsSuccess);
    }

    [TestMethod]
    public void DeviceGroupPermissions()
    {
        Assert.AreEqual(2, _deviceListingService.GetDevicesForUser(_testData.Org1Admin1.UserName!).Length);
        Assert.AreEqual(2, _deviceListingService.GetDevicesForUser(_testData.Org1Admin2.UserName!).Length);
        Assert.AreEqual(0, _deviceListingService.GetDevicesForUser(_testData.Org1User1.UserName!).Length);
        Assert.AreEqual(0, _deviceListingService.GetDevicesForUser(_testData.Org1User2.UserName!).Length);
        Assert.AreEqual(0, _deviceListingService.GetDevicesForUser(_testData.Org2User1.UserName!).Length);
        Assert.AreEqual(0, _deviceListingService.GetDevicesForUser(_testData.Org2User2.UserName!).Length);

        Assert.IsTrue(_userService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org1Admin1));
        Assert.IsTrue(_userService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org1Admin2));
        Assert.IsFalse(_userService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org1User1));
        Assert.IsFalse(_userService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org1User2));
        Assert.IsFalse(_userService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org2User1));
        Assert.IsFalse(_userService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org2User2));

        var groupID = _testData.Org1Group1.ID;
        _deviceGroupMembershipService.AddUserToDeviceGroup(_testData.Org1Id, groupID, _testData.Org1User1.UserName!, out _);
        _testData.Org1Device1.DeviceGroupID = groupID;
        _deviceService.UpdateDevice(_testData.Org1Device1.ID, "", "", groupID, "");

        Assert.AreEqual(2, _deviceListingService.GetDevicesForUser(_testData.Org1Admin1.UserName!).Length);
        Assert.AreEqual(2, _deviceListingService.GetDevicesForUser(_testData.Org1Admin2.UserName!).Length);
        Assert.AreEqual(1, _deviceListingService.GetDevicesForUser(_testData.Org1User1.UserName!).Length);
        Assert.AreEqual(0, _deviceListingService.GetDevicesForUser(_testData.Org1User2.UserName!).Length);
        Assert.AreEqual(0, _deviceListingService.GetDevicesForUser(_testData.Org2User1.UserName!).Length);
        Assert.AreEqual(0, _deviceListingService.GetDevicesForUser(_testData.Org2User2.UserName!).Length);

        Assert.IsTrue(_userService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org1Admin1));
        Assert.IsTrue(_userService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org1Admin2));
        Assert.IsTrue(_userService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org1User1));
        Assert.IsFalse(_userService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org1User2));
        Assert.IsFalse(_userService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org2User1));
        Assert.IsFalse(_userService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org2User2));

        var allDevices = _deviceListingService.GetAllDevices(_testData.Org1Id).Select(x => x.ID).ToArray();
        Assert.AreEqual(2, _userService.FilterDeviceIdsByUserPermission(allDevices, _testData.Org1Admin1).Length);
        Assert.AreEqual(2, _userService.FilterDeviceIdsByUserPermission(allDevices, _testData.Org1Admin2).Length);
        Assert.AreEqual(1, _userService.FilterDeviceIdsByUserPermission(allDevices, _testData.Org1User1).Length);
        Assert.AreEqual(0, _userService.FilterDeviceIdsByUserPermission(allDevices, _testData.Org1User2).Length);
        Assert.AreEqual(0, _userService.FilterDeviceIdsByUserPermission(allDevices, _testData.Org2User1).Length);
        Assert.AreEqual(0, _userService.FilterDeviceIdsByUserPermission(allDevices, _testData.Org2User2).Length);
    }

    [TestMethod]
    public async Task GetPendingScriptRuns_GivenMultipleRunsQueued_ReturnsOnlyLatest()
    {
        var now = Time.Now;

        var savedScript = new SavedScript()
        {
            Content = "Get-ChildItem",
            Creator = _testData.Org1Admin1,
            CreatorId = _testData.Org1Admin1.Id,
            Name = "GCI",
            Organization = _testData.Org1Admin1.Organization,
            OrganizationID = _testData.Org1Id,
            Shell = ScriptingShell.PSCore
        };

        await _savedScriptService.AddOrUpdateSavedScript(savedScript, _testData.Org1Admin1.Id);

        var scriptRun = new ScriptRun()
        {
            Devices = new() { _testData.Org1Device1 },
            InputType = ScriptInputType.ScheduledScript,
            SavedScriptId = savedScript.Id,
            Initiator = _testData.Org1Admin1.UserName,
            RunAt = now,
            OrganizationID = _testData.Org1Id,
            Organization = _testData.Org1Admin1.Organization,
            RunOnNextConnect = true
        };

        await _scriptResultService.AddScriptRun(scriptRun);

        scriptRun.Id = 0;
        scriptRun.RunAt = now.AddMinutes(1);

        await _scriptResultService.AddScriptRun(scriptRun);

        Time.Adjust(TimeSpan.FromMinutes(2));

        var pendingRuns = await _scriptResultService.GetPendingScriptRuns(_testData.Org1Device1.ID);

        Assert.AreEqual(1, pendingRuns.Count());
        Assert.AreEqual(2, pendingRuns.First().Id);

        var dto = new ScriptResultDto()
        {
            DeviceID = _testData.Org1Device1.ID,
            InputType = ScriptInputType.ScheduledScript,
            SavedScriptId = savedScript.Id,
            ScriptRunId = scriptRun.Id,
            Shell = ScriptingShell.PSCore,
            ScriptInput = "echo test"
        };

        var scriptResult = (await _scriptResultService.AddScriptResult(dto)).Value!;

        await _scriptResultService.AddScriptResultToScriptRun(scriptResult.ID, scriptRun.Id);

        pendingRuns = await _scriptResultService.GetPendingScriptRuns(_testData.Org1Device1.ID);

        Assert.AreEqual(0, pendingRuns.Count());
    }


    [TestCleanup]
    public void TestCleanup()
    {
        _testData.ClearData();
    }

    [TestInitialize]
    public async Task TestInit()
    {
        _testData = new TestData();
        await _testData.Init();
        _alertService = IoCActivator.ServiceProvider.GetRequiredService<IAlertService>();
        _apiTokenService = IoCActivator.ServiceProvider.GetRequiredService<IApiTokenService>();
        _deviceGroupMembershipService = IoCActivator.ServiceProvider.GetRequiredService<IDeviceGroupMembershipService>();
        _deviceListingService = IoCActivator.ServiceProvider.GetRequiredService<IDeviceListingService>();
        _deviceService = IoCActivator.ServiceProvider.GetRequiredService<IDeviceService>();
        _organizationService = IoCActivator.ServiceProvider.GetRequiredService<IOrganizationService>();
        _savedScriptService = IoCActivator.ServiceProvider.GetRequiredService<ISavedScriptService>();
        _scriptResultService = IoCActivator.ServiceProvider.GetRequiredService<IScriptResultService>();
        _userService = IoCActivator.ServiceProvider.GetRequiredService<IUserService>();
    }

    [TestMethod]
    public async Task UpdateOrganizationName()
    {
        Assert.AreEqual("Org1", _testData.Org1Admin1.Organization!.OrganizationName);
        await _organizationService.UpdateOrganizationName(_testData.Org1Id, "Test Org");
        var updatedOrg = (await _organizationService.GetOrganizationById(_testData.Org1Id)).Value;
        Assert.AreEqual("Test Org", updatedOrg!.OrganizationName);
    }

    [TestMethod]
    public async Task UpdateServerAdmins()
    {
        var currentAdmins = _userService.GetServerAdmins();

        Assert.AreEqual(1, currentAdmins.Count);

        await _userService.SetIsServerAdmin(_testData.Org1Admin2.Id, true, _testData.Org1Admin1.Id);

        currentAdmins = _userService.GetServerAdmins();
        Assert.AreEqual(2, currentAdmins.Count);
        Assert.IsTrue(currentAdmins.Contains(_testData.Org1Admin1.UserName!));
        Assert.IsTrue(currentAdmins.Contains(_testData.Org1Admin2.UserName!));

        // Shouldn't be able to change themselves.
        await _userService.SetIsServerAdmin(_testData.Org1Admin2.Id, false, _testData.Org1Admin2.Id);
        currentAdmins = _userService.GetServerAdmins();
        Assert.AreEqual(2, currentAdmins.Count);

        // Non-admins shouldn't be able to change admins.
        await _userService.SetIsServerAdmin(_testData.Org1User1.Id, false, _testData.Org1Admin2.Id);
        currentAdmins = _userService.GetServerAdmins();
        Assert.AreEqual(2, currentAdmins.Count);

        await _userService.SetIsServerAdmin(_testData.Org1Admin2.Id, false, _testData.Org1Admin1.Id);
        currentAdmins = _userService.GetServerAdmins();
        Assert.AreEqual(1, currentAdmins.Count);
        Assert.AreEqual(_testData.Org1Admin1.UserName, currentAdmins[0]);
    }

    [TestMethod]
    public async Task VerifyInitialData()
    {
        Assert.IsNotNull((await _userService.GetUserByName(_testData.Org1Admin1.UserName!)).Value);
        Assert.IsNotNull((await _userService.GetUserByName(_testData.Org1Admin2.UserName!)).Value);
        Assert.IsNotNull((await _userService.GetUserByName(_testData.Org1User1.UserName!)).Value);
        Assert.IsNotNull((await _userService.GetUserByName(_testData.Org1User2.UserName!)).Value);
        Assert.AreEqual(2, _organizationService.GetOrganizationCount());

        var devices1 = _deviceListingService.GetAllDevices(_testData.Org1Id);
        Assert.AreEqual(2, devices1.Length);
        Assert.IsTrue(devices1.Any(x => x.ID == "Org1Device1"));
        Assert.IsTrue(devices1.Any(x => x.ID == "Org1Device2"));

        var devices2 = _deviceListingService.GetAllDevices(_testData.Org2Id);
        Assert.AreEqual(2, devices2.Length);
        Assert.IsTrue(devices2.Any(x => x.ID == "Org2Device1"));
        Assert.IsTrue(devices2.Any(x => x.ID == "Org2Device2"));

        var org1Ids = new string[]
        {
            _testData.Org1Group1.OrganizationID,
            _testData.Org1Group2.OrganizationID,
            _testData.Org1Admin1.OrganizationID,
            _testData.Org1Admin2.OrganizationID,
            _testData.Org1User1.OrganizationID,
            _testData.Org1User2.OrganizationID,
            _testData.Org1Device1.OrganizationID,
            _testData.Org1Device2.OrganizationID
        };

        Assert.IsTrue(org1Ids.All(x => x == _testData.Org1Id));

        var org2Ids = new string[]
{
            _testData.Org2Group1.OrganizationID,
            _testData.Org2Group2.OrganizationID,
            _testData.Org2Admin1.OrganizationID,
            _testData.Org2Admin2.OrganizationID,
            _testData.Org2User1.OrganizationID,
            _testData.Org2User2.OrganizationID,
            _testData.Org2Device1.OrganizationID,
            _testData.Org2Device2.OrganizationID
};

        Assert.IsTrue(org2Ids.All(x => x == _testData.Org2Id));
    }

    [TestMethod]
    public async Task ValidateApiKey_DoesNotRecordLastUsedWhenSecretIsWrong()
    {
        const string keyId = "ApiKeyFailureTest";

        await AddApiToken(keyId, "correct-secret");

        var isValid = await _apiTokenService.ValidateApiKey(
            keyId,
            "wrong-secret",
            "/api/scripting",
            "127.0.0.1");

        Assert.IsFalse(isValid);
        Assert.IsNull(await GetLastUsed(keyId),
            "A rejected attempt must not be recorded as key usage.");
    }

    [TestMethod]
    public async Task ValidateApiKey_RecordsLastUsedWhenSecretIsCorrect()
    {
        const string keyId = "ApiKeySuccessTest";

        await AddApiToken(keyId, "correct-secret");

        var isValid = await _apiTokenService.ValidateApiKey(
            keyId,
            "correct-secret",
            "/api/scripting",
            "127.0.0.1");

        Assert.IsTrue(isValid);
        Assert.IsNotNull(await GetLastUsed(keyId),
            "A successful authentication must be recorded.");
    }

    private async Task AddApiToken(string keyId, string secret)
    {
        using var scope = IoCActivator.ServiceProvider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<AppDb>();

        dbContext.ApiTokens.Add(new ApiToken
        {
            ID = keyId,
            Name = keyId,
            OrganizationID = _testData.Org1Id,
            Secret = new PasswordHasher<string>().HashPassword(string.Empty, secret)
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task<DateTimeOffset?> GetLastUsed(string keyId)
    {
        using var scope = IoCActivator.ServiceProvider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<AppDb>();

        var token = await dbContext.ApiTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ID == keyId);

        Assert.IsNotNull(token, "The test token should still exist.");

        return token.LastUsed;
    }
}
