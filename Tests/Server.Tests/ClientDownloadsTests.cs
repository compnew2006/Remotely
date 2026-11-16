using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Remotely.Server.API;
using Remotely.Server.Auth;
using Remotely.Server.Services;
using Remotely.Shared.Services;

namespace Remotely.Server.Tests;

[TestClass]
public class ClientDownloadsTests
{
    [TestMethod]
    [Description("Anonymous browser requests for portable desktop clients redirect directly to the raw download instead of returning 401.")]
    public async Task GetDesktop_AnonymousBrowser_RedirectsToRawContent()
    {
        using var tempRoot = new TempWebRoot();
        tempRoot.CreateFile("Content", "Win-x64", "Remotely_Desktop.exe", "portable-exe");

        var (controller, organizationService, settingsService) = CreateController(tempRoot.Root);
        controller.ControllerContext.HttpContext.Request.Headers.Accept = "text/html,application/xhtml+xml";

        var result = await controller.GetDesktop("WindowsDesktop-x64", "org-id");

        Assert.IsInstanceOfType(result, typeof(RedirectResult));
        var redirect = (RedirectResult)result!;
        Assert.AreEqual("/Content/Win-x64/Remotely_Desktop.exe", redirect.Url);
        organizationService.VerifyNoOtherCalls();
        settingsService.VerifyNoOtherCalls();
    }

    [TestMethod]
    [Description("Anonymous API requests for portable desktop clients fail closed with 401 instead of being redirected.")]
    public async Task GetDesktop_AnonymousApiClient_ReturnsUnauthorized()
    {
        using var tempRoot = new TempWebRoot();
        tempRoot.CreateFile("Content", "Win-x64", "Remotely_Desktop.exe", "portable-exe");

        var (controller, organizationService, settingsService) = CreateController(tempRoot.Root);
        controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

        var result = await controller.GetDesktop("WindowsDesktop-x64", "org-id");

        Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
        organizationService.VerifyNoOtherCalls();
        settingsService.VerifyNoOtherCalls();
    }

    private static (
        ClientDownloadsController Controller,
        Mock<IOrganizationService> OrganizationService,
        Mock<ISettingsService> SettingsService) CreateController(string webRootPath)
    {
        var hostEnv = new Mock<IWebHostEnvironment>(MockBehavior.Strict);
        hostEnv
            .Setup(x => x.WebRootPath)
            .Returns(webRootPath);

        var embeddedServerDataProvider = new Mock<IEmbeddedServerDataProvider>(MockBehavior.Strict);
        var organizationService = new Mock<IOrganizationService>(MockBehavior.Strict);
        var settingsService = new Mock<ISettingsService>(MockBehavior.Strict);
        var controllerLogger = new Mock<ILogger<ClientDownloadsController>>();
        var filterLogger = new Mock<ILogger<ApiAuthorizationFilter>>();
        var apiAuthorizationFilter = new ApiAuthorizationFilter(
            new Mock<IApiTokenService>(MockBehavior.Strict).Object,
            new Mock<IUserService>(MockBehavior.Strict).Object,
            filterLogger.Object);

        var controller = new ClientDownloadsController(
            hostEnv.Object,
            embeddedServerDataProvider.Object,
            organizationService.Object,
            settingsService.Object,
            controllerLogger.Object,
            apiAuthorizationFilter)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        return (controller, organizationService, settingsService);
    }

    private sealed class TempWebRoot : IDisposable
    {
        public string Root { get; }

        public TempWebRoot()
        {
            Root = Path.Combine(Path.GetTempPath(), "Remotely.Server.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public void CreateFile(string directory, string subdirectory, string fileName, string contents)
        {
            var path = Path.Combine(Root, directory, subdirectory);
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, fileName), contents);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
