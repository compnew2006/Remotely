using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Remotely.Server.Data;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

/// <summary>
/// The camera clips of a device are stored in that device's own folder, the
/// file names of the clips can never reach outside of it, and the clips are
/// removed when they are deleted or cleared.
/// </summary>
[TestClass]
public class CameraServiceTests
{
    private CameraService _cameraService;
    private Device _device;
    private string _contentRoot;
    private TestData _testData;

    [TestInitialize]
    public async Task Init()
    {
        _testData = new TestData();
        await _testData.Init();

        _device = _testData.Org1Device1;

        _contentRoot = Path.Combine(
            Path.GetTempPath(),
            "RemotelyCameraTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_contentRoot);

        var hostEnvironment = new Mock<IWebHostEnvironment>();
        hostEnvironment.Setup(x => x.ContentRootPath).Returns(_contentRoot);

        var dbFactory = IoCActivator.ServiceProvider.GetRequiredService<IAppDbFactory>();

        _cameraService = new CameraService(
            dbFactory,
            hostEnvironment.Object,
            NullLogger<CameraService>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_contentRoot))
            {
                Directory.Delete(_contentRoot, true);
            }
        }
        catch
        {
            // The temporary folder is left behind when it can't be removed.
        }
    }

    [TestMethod]
    [Description("FR-019: a file name that tries to leave the device's folder is refused.")]
    public void GetClipPath_TraversalFileName_ReturnsNull()
    {
        Assert.IsNull(_cameraService.GetClipPath(_device, "../secret.mp4"));
        Assert.IsNull(_cameraService.GetClipPath(_device, "sub/clip.mp4"));
        Assert.IsNull(_cameraService.GetClipPath(_device, "..\\clip.mp4"));
        Assert.IsNull(_cameraService.GetClipPath(_device, string.Empty));
    }

    [TestMethod]
    [Description("FR-019: a file that does not exist has no path.")]
    public void GetClipPath_FileThatDoesNotExist_ReturnsNull()
    {
        Assert.IsNull(_cameraService.GetClipPath(_device, "20261106-000000000.mp4"));
    }

    [TestMethod]
    [Description("FR-012: an upload id that is not a GUID is refused.")]
    public void BeginUpload_ClipIdThatIsNotAGuid_ReturnsNull()
    {
        Assert.IsNull(_cameraService.BeginUpload(_device, "not-a-guid"));
        Assert.IsNull(_cameraService.BeginUpload(_device, "../../evil"));
    }

    [TestMethod]
    [Description("FR-012: an upload with a valid id starts in the device's own folder.")]
    public void BeginUpload_ValidClipId_StartsInTheDeviceFolder()
    {
        var clipId = Guid.NewGuid().ToString();

        var path = _cameraService.BeginUpload(_device, clipId);

        Assert.IsNotNull(path);
        Assert.IsTrue(path!.StartsWith(_contentRoot, StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(path.Contains(_device.ID, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [Description("FR-003: a device id that could escape the camera folder is refused.")]
    public void BeginUpload_DeviceIdWithPathCharacters_ReturnsNull()
    {
        var unsafeDevice = new Device { ID = "../other-device" };
        Assert.IsNull(_cameraService.BeginUpload(unsafeDevice, Guid.NewGuid().ToString()));
    }

    [TestMethod]
    [Description("FR-012/FR-018: the uploaded clip is stored with its metadata.")]
    public async Task CompleteUpload_StoresTheClipAndItsMetadata()
    {
        var clipId = Guid.NewGuid().ToString();
        var contents = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        _cameraService.BeginUpload(_device, clipId);
        Assert.IsTrue(_cameraService.AppendChunk(clipId, contents));

        var clip = await _cameraService.CompleteUpload(
            _device,
            clipId,
            "High",
            30,
            "Test Camera");

        Assert.IsNotNull(clip);
        Assert.AreEqual(contents.Length, clip!.SizeBytes);
        Assert.AreEqual("High", clip.Quality);
        Assert.AreEqual(30, clip.DurationSeconds);
        Assert.AreEqual("Test Camera", clip.CameraName);
        Assert.AreEqual(_device.ID, clip.DeviceID);

        var path = _cameraService.GetClipPath(_device, clip.FileName);
        Assert.IsNotNull(path);
        Assert.IsTrue(File.Exists(path));

        var stored = _cameraService.GetClips(_device.ID, 10);
        Assert.AreEqual(1, stored.Length);
        Assert.AreEqual(clip.FileName, stored[0].FileName);
    }

    [TestMethod]
    [Description("FR-013: a chunk that is sent without an upload is refused.")]
    public void AppendChunk_WithoutAnUpload_IsRefused()
    {
        Assert.IsFalse(_cameraService.AppendChunk(Guid.NewGuid().ToString(), new byte[] { 1 }));
        Assert.IsFalse(_cameraService.AppendChunk(Guid.NewGuid().ToString(), Array.Empty<byte>()));
    }

    [TestMethod]
    [Description("FR-022: deleting a clip removes the file and the metadata.")]
    public async Task DeleteClip_RemovesTheFileAndTheMetadata()
    {
        var clip = await SaveClipAsync();

        var path = _cameraService.GetClipPath(_device, clip.FileName);
        Assert.IsNotNull(path);

        _cameraService.DeleteClip(_device, clip.FileName);

        Assert.IsFalse(File.Exists(path));
        Assert.AreEqual(0, _cameraService.GetClips(_device.ID, 10).Length);
    }

    [TestMethod]
    [Description("FR-019: a traversal file name is not deleted.")]
    public void DeleteClip_TraversalFileName_DoesNothing()
    {
        var clip = _cameraService.GetClips(_device.ID, 10).FirstOrDefault();
        Assert.IsNull(clip);

        _cameraService.DeleteClip(_device, "../secret.mp4");

        Assert.AreEqual(0, _cameraService.GetClips(_device.ID, 10).Length);
    }

    [TestMethod]
    [Description("FR-022: clearing a device removes every clip of that device.")]
    public async Task ClearClips_RemovesEveryClipOfTheDevice()
    {
        await SaveClipAsync();
        await SaveClipAsync();

        Assert.AreEqual(2, _cameraService.GetClips(_device.ID, 10).Length);

        _cameraService.ClearClips(_device);

        Assert.AreEqual(0, _cameraService.GetClips(_device.ID, 10).Length);

        var directory = _cameraService.GetCameraDirectory(_device);
        if (Directory.Exists(directory))
        {
            Assert.AreEqual(0, Directory.GetFiles(directory, "*.mp4").Length);
        }
    }

    [TestMethod]
    [Description("FR-016: the clips of a device are stored in that device's own folder.")]
    public void GetCameraDirectory_UsesTheDeviceId()
    {
        var directory = _cameraService.GetCameraDirectory(_device);

        Assert.IsTrue(directory.Contains(_device.ID, StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(directory.StartsWith(_contentRoot, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [Description("Camera settings can place clips and uploads in a per-device custom directory.")]
    public void GetCameraDirectory_CustomDirectory_ReturnsConfiguredDirectory()
    {
        var customDirectory = Path.Combine(_contentRoot, "Custom Camera Clips");
        _device.CameraDirectory = customDirectory;

        var directory = _cameraService.GetCameraDirectory(_device);
        var uploadPath = _cameraService.BeginUpload(_device, Guid.NewGuid().ToString());

        Assert.AreEqual(customDirectory, directory);
        Assert.IsNotNull(uploadPath);
        Assert.IsTrue(uploadPath!.StartsWith(customDirectory, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [Description("Camera retention settings limit the number of clips kept for a device.")]
    public async Task DeleteExpiredClips_UsesDeviceRetentionCount()
    {
        _device.CameraRetentionCount = 1;

        await SaveClipAsync();
        await SaveClipAsync();

        Assert.AreEqual(1, _cameraService.GetClips(_device.ID, 10).Length);
    }

    [TestMethod]
    [Description("FR-006: the clips of one device are not returned for another device.")]
    public async Task GetClips_AnotherDevice_DoesNotReturnTheClips()
    {
        await SaveClipAsync();

        var otherDevice = _testData.Org1Device2;

        Assert.AreEqual(1, _cameraService.GetClips(_device.ID, 10).Length);
        Assert.AreEqual(0, _cameraService.GetClips(otherDevice.ID, 10).Length);
    }

    private async Task<Shared.Entities.CameraClip> SaveClipAsync()
    {
        var clip = await _cameraService.SaveClip(
            _device,
            new byte[] { 1, 2, 3 },
            "Medium",
            30,
            "Test Camera");

        Assert.IsNotNull(clip);

        return clip!;
    }
}
