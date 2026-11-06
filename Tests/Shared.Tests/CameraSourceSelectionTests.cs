using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;

namespace Remotely.Shared.Tests;

/// <summary>
/// The video and the audio sources of a recording travel as one string that
/// holds the kind of the source and its device name, so a camera can never
/// be mixed up with a display.
/// </summary>
[TestClass]
public class CameraSourceSelectionTests
{
    [TestMethod]
    public void GetSourceValue_KeepsTheKindAndTheId()
    {
        var value = CameraSourceSelection.GetSourceValue(
            new CameraDeviceInfo("webcam-1", "Front Camera", CameraDeviceKind.Camera));

        var (kind, id) = CameraSourceSelection.ParseSource(value);

        Assert.AreEqual(CameraDeviceKind.Camera, kind);
        Assert.AreEqual("webcam-1", id);
    }

    [TestMethod]
    public void GetSourceValue_ADisplayKeepsItsKind()
    {
        var value = CameraSourceSelection.GetSourceValue(
            new CameraDeviceInfo(@"\\.\DISPLAY1", "Screen 1 (1920x1080)", CameraDeviceKind.Display));

        var (kind, id) = CameraSourceSelection.ParseSource(value);

        Assert.AreEqual(CameraDeviceKind.Display, kind);
        Assert.AreEqual(@"\\.\DISPLAY1", id);
    }

    [TestMethod]
    public void ParseSource_ValueWithoutAKind_FallsBackToACamera()
    {
        var (kind, id) = CameraSourceSelection.ParseSource("webcam-1");

        Assert.AreEqual(CameraDeviceKind.Camera, kind);
        Assert.AreEqual("webcam-1", id);
    }

    [TestMethod]
    public void ParseSource_UnknownKind_FallsBackToACamera()
    {
        var (kind, _) = CameraSourceSelection.ParseSource("SomethingElse|webcam-1");

        Assert.AreEqual(CameraDeviceKind.Camera, kind);
    }

    [TestMethod]
    public void ParseSource_EmptyValue_ReturnsAnEmptyCamera()
    {
        var (kind, id) = CameraSourceSelection.ParseSource(string.Empty);

        Assert.AreEqual(CameraDeviceKind.Camera, kind);
        Assert.AreEqual(string.Empty, id);
    }

    [TestMethod]
    public void GetAudioValue_NullDevice_MeansNoAudio()
    {
        var value = CameraSourceSelection.GetAudioValue(null);

        var (source, id) = CameraSourceSelection.ParseAudio(value);

        Assert.AreEqual(CameraAudioSource.None, source);
        Assert.AreEqual(string.Empty, id);
    }

    [TestMethod]
    public void GetAudioValue_AMicrophoneKeepsItsKindAndId()
    {
        var value = CameraSourceSelection.GetAudioValue(
            new CameraAudioDeviceInfo("mic-1", "Headset", CameraAudioSource.Microphone));

        var (source, id) = CameraSourceSelection.ParseAudio(value);

        Assert.AreEqual(CameraAudioSource.Microphone, source);
        Assert.AreEqual("mic-1", id);
    }

    [TestMethod]
    public void GetAudioValue_ALoopbackDeviceKeepsItsKindAndId()
    {
        var value = CameraSourceSelection.GetAudioValue(
            new CameraAudioDeviceInfo("speakers", "Speakers (Realtek)", CameraAudioSource.SystemAudio));

        var (source, id) = CameraSourceSelection.ParseAudio(value);

        Assert.AreEqual(CameraAudioSource.SystemAudio, source);
        Assert.AreEqual("speakers", id);
    }

    [TestMethod]
    public void ParseAudio_UnknownKind_MeansNoAudio()
    {
        var (source, _) = CameraSourceSelection.ParseAudio("SomethingElse|mic-1");

        Assert.AreEqual(CameraAudioSource.None, source);
    }

    [TestMethod]
    public void ParseAudio_EmptyValue_MeansNoAudio()
    {
        var (source, id) = CameraSourceSelection.ParseAudio(string.Empty);

        Assert.AreEqual(CameraAudioSource.None, source);
        Assert.AreEqual(string.Empty, id);
    }

    [TestMethod]
    public void CameraCaptureRequest_CarriesTheSources()
    {
        var request = new CameraCaptureRequest(
            @"\\.\DISPLAY1",
            CameraQuality.High,
            45,
            CameraDeviceKind.Display,
            CameraAudioSource.SystemAudio,
            "speakers");

        Assert.AreEqual(@"\\.\DISPLAY1", request.CameraId);
        Assert.AreEqual(CameraQuality.High, request.Quality);
        Assert.AreEqual(45, request.DurationSeconds);
        Assert.AreEqual(CameraDeviceKind.Display, request.VideoSource);
        Assert.AreEqual(CameraAudioSource.SystemAudio, request.AudioSource);
        Assert.AreEqual("speakers", request.AudioDeviceId);
    }

    [TestMethod]
    public void CameraCaptureRequest_DefaultsToACameraWithoutAudio()
    {
        var request = new CameraCaptureRequest();

        Assert.AreEqual(CameraDeviceKind.Camera, request.VideoSource);
        Assert.AreEqual(CameraAudioSource.None, request.AudioSource);
        Assert.AreEqual(string.Empty, request.AudioDeviceId);
    }
}
