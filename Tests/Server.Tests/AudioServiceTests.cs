using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Remotely.Server.Data;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using Remotely.Shared.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

/// <summary>
/// The audio clips of a device are stored in that device's own folder, the
/// file names of the clips can never reach outside of it, and the clips are
/// removed when they are deleted or cleared.
/// </summary>
[TestClass]
public class AudioServiceTests
{
    private AudioService _audioService;
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
            "RemotelyAudioTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_contentRoot);

        var hostEnvironment = new Mock<IWebHostEnvironment>();
        hostEnvironment.Setup(x => x.ContentRootPath).Returns(_contentRoot);

        var dbFactory = IoCActivator.ServiceProvider.GetRequiredService<IAppDbFactory>();

        _audioService = new AudioService(
            dbFactory,
            hostEnvironment.Object,
            NullLogger<AudioService>.Instance);
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
    [Description("A file name that tries to leave the device's folder is refused.")]
    public void GetClipPath_TraversalFileName_ReturnsNull()
    {
        Assert.IsNull(_audioService.GetClipPath(_device, "../secret.wav"));
        Assert.IsNull(_audioService.GetClipPath(_device, "sub/clip.wav"));
        Assert.IsNull(_audioService.GetClipPath(_device, "..\\clip.wav"));
        Assert.IsNull(_audioService.GetClipPath(_device, string.Empty));
    }

    [TestMethod]
    [Description("A file that does not exist has no path.")]
    public void GetClipPath_FileThatDoesNotExist_ReturnsNull()
    {
        Assert.IsNull(_audioService.GetClipPath(_device, "20261106-000000000.wav"));
    }

    [TestMethod]
    [Description("An upload id that is not a GUID is refused.")]
    public void BeginUpload_ClipIdThatIsNotAGuid_ReturnsNull()
    {
        Assert.IsNull(_audioService.BeginUpload(_device, "not-a-guid"));
        Assert.IsNull(_audioService.BeginUpload(_device, "../../evil"));
    }

    [TestMethod]
    [Description("An upload with a valid id starts in the device's own folder.")]
    public void BeginUpload_ValidClipId_StartsInTheDeviceFolder()
    {
        var clipId = Guid.NewGuid().ToString();

        var path = _audioService.BeginUpload(_device, clipId);

        Assert.IsNotNull(path);
        Assert.IsTrue(path!.StartsWith(_contentRoot, StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(path.Contains(_device.ID, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [Description("A device id that could escape the audio folder is refused.")]
    public void BeginUpload_DeviceIdWithPathCharacters_ReturnsNull()
    {
        var unsafeDevice = new Device { ID = "../other-device" };
        Assert.IsNull(_audioService.BeginUpload(unsafeDevice, Guid.NewGuid().ToString()));
    }

    [TestMethod]
    [Description("The uploaded clip is stored with its metadata.")]
    public async Task CompleteUpload_StoresTheClipAndItsMetadata()
    {
        var clipId = Guid.NewGuid().ToString();
        var contents = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        _audioService.BeginUpload(_device, clipId);
        Assert.IsTrue(_audioService.AppendChunk(clipId, contents));

        var clip = await _audioService.CompleteUpload(
            _device,
            clipId,
            AudioQuality.High,
            30,
            AudioSource.Microphone);

        Assert.IsNotNull(clip);
        Assert.AreEqual(contents.Length, clip!.SizeBytes);
        Assert.AreEqual("High", clip.Quality);
        Assert.AreEqual(30, clip.DurationSeconds);
        Assert.AreEqual("Microphone", clip.Source);
        Assert.AreEqual(_device.ID, clip.DeviceID);

        var path = _audioService.GetClipPath(_device, clip.FileName);
        Assert.IsNotNull(path);
        Assert.IsTrue(File.Exists(path));

        var stored = _audioService.GetClips(_device.ID, 10);
        Assert.AreEqual(1, stored.Length);
        Assert.AreEqual(clip.FileName, stored[0].FileName);
    }

    [TestMethod]
    [Description("A chunk that is sent without an upload is refused.")]
    public void AppendChunk_WithoutAnUpload_IsRefused()
    {
        Assert.IsFalse(_audioService.AppendChunk(Guid.NewGuid().ToString(), new byte[] { 1 }));
        Assert.IsFalse(_audioService.AppendChunk(Guid.NewGuid().ToString(), Array.Empty<byte>()));
    }

    [TestMethod]
    [Description("Deleting a clip removes the file and the metadata.")]
    public async Task DeleteClip_RemovesTheFileAndTheMetadata()
    {
        var clip = await SaveClipAsync();

        var path = _audioService.GetClipPath(_device, clip.FileName);
        Assert.IsNotNull(path);

        _audioService.DeleteClip(_device, clip.FileName);

        Assert.IsFalse(File.Exists(path));
        Assert.AreEqual(0, _audioService.GetClips(_device.ID, 10).Length);
    }

    [TestMethod]
    [Description("A traversal file name is not deleted.")]
    public void DeleteClip_TraversalFileName_DoesNothing()
    {
        var clip = _audioService.GetClips(_device.ID, 10).FirstOrDefault();
        Assert.IsNull(clip);

        _audioService.DeleteClip(_device, "../secret.wav");

        Assert.AreEqual(0, _audioService.GetClips(_device.ID, 10).Length);
    }

    [TestMethod]
    [Description("Clearing a device removes every clip of that device.")]
    public async Task ClearClips_RemovesEveryClipOfTheDevice()
    {
        await SaveClipAsync();
        await SaveClipAsync();

        Assert.AreEqual(2, _audioService.GetClips(_device.ID, 10).Length);

        _audioService.ClearClips(_device);

        Assert.AreEqual(0, _audioService.GetClips(_device.ID, 10).Length);

        var directory = _audioService.GetAudioDirectory(_device);
        if (Directory.Exists(directory))
        {
            Assert.AreEqual(0, Directory.GetFiles(directory, "*.wav").Length);
        }
    }

    [TestMethod]
    [Description("The clips of a device are stored in that device's own folder.")]
    public void GetAudioDirectory_UsesTheDeviceId()
    {
        var directory = _audioService.GetAudioDirectory(_device);

        Assert.IsTrue(directory.Contains(_device.ID, StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(directory.StartsWith(_contentRoot, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [Description("Audio settings can place clips and uploads in a per-device custom directory.")]
    public void GetAudioDirectory_CustomDirectory_ReturnsConfiguredDirectory()
    {
        var customDirectory = Path.Combine(_contentRoot, "Custom Audio Clips");
        _device.AudioDirectory = customDirectory;

        var directory = _audioService.GetAudioDirectory(_device);
        var uploadPath = _audioService.BeginUpload(_device, Guid.NewGuid().ToString());

        Assert.AreEqual(customDirectory, directory);
        Assert.IsNotNull(uploadPath);
        Assert.IsTrue(uploadPath!.StartsWith(customDirectory, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [Description("Audio retention settings limit the number of clips kept for a device.")]
    public async Task DeleteExpiredClips_UsesDeviceRetentionCount()
    {
        _device.AudioRetentionCount = 1;

        await SaveClipAsync();
        await SaveClipAsync();

        Assert.AreEqual(1, _audioService.GetClips(_device.ID, 10).Length);
    }

    [TestMethod]
    [Description("The clips of one device are not returned for another device.")]
    public async Task GetClips_AnotherDevice_DoesNotReturnTheClips()
    {
        await SaveClipAsync();

        var otherDevice = _testData.Org1Device2;

        Assert.AreEqual(1, _audioService.GetClips(_device.ID, 10).Length);
        Assert.AreEqual(0, _audioService.GetClips(otherDevice.ID, 10).Length);
    }

    private async Task<AudioClip> SaveClipAsync()
    {
        var clip = await _audioService.SaveClip(
            _device,
            new byte[] { 1, 2, 3 },
            AudioQuality.Medium,
            30,
            AudioSource.SystemAudio);

        Assert.IsNotNull(clip);

        return clip!;
    }
}
