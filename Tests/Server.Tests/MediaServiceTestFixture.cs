using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Remotely.Server.Data;
using Remotely.Shared.Entities;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

internal sealed class MediaServiceTestFixture : IDisposable
{
    private MediaServiceTestFixture(
        Device device,
        Device otherDevice,
        string contentRoot,
        IWebHostEnvironment hostEnvironment,
        IAppDbFactory dbFactory)
    {
        Device = device;
        OtherDevice = otherDevice;
        ContentRoot = contentRoot;
        HostEnvironment = hostEnvironment;
        DbFactory = dbFactory;
    }

    public Device Device { get; }

    public Device OtherDevice { get; }

    public string ContentRoot { get; }

    public IWebHostEnvironment HostEnvironment { get; }

    public IAppDbFactory DbFactory { get; }

    public static async Task<MediaServiceTestFixture> Create(string testFolderName)
    {
        var testData = new TestData();
        await testData.Init();

        var contentRoot = Path.Combine(
            Path.GetTempPath(),
            testFolderName,
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(contentRoot);

        var hostEnvironment = new Mock<IWebHostEnvironment>();
        hostEnvironment.Setup(x => x.ContentRootPath).Returns(contentRoot);

        var dbFactory = IoCActivator.ServiceProvider.GetRequiredService<IAppDbFactory>();

        return new MediaServiceTestFixture(
            testData.Org1Device1,
            testData.Org1Device2,
            contentRoot,
            hostEnvironment.Object,
            dbFactory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(ContentRoot))
            {
                Directory.Delete(ContentRoot, true);
            }
        }
        catch
        {
            // Leave the temporary folder when it can't be removed.
        }
    }
}
