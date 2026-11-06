using Remotely.Desktop.Shared.Abstractions;
using Remotely.Desktop.Shared.Services;
using Remotely.Desktop.Shared.ViewModels;
using Remotely.Desktop.UI.Controls.Dialogs;
using Remotely.Desktop.UI.Services;
using Remotely.Desktop.UI.Views;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Remotely.Shared.Extensions;
using Remotely.Shared.Helpers;
using System.IO;

namespace Remotely.Desktop.Win.Services;

public class FileTransferServiceWin : IFileTransferService
{
    private static readonly ConcurrentDictionary<string, FileTransferWindow> _fileTransferWindows =
        new();

    private static readonly ConcurrentDictionary<string, FileStream> _partialTransfers =
        new();

    private static readonly SemaphoreSlim _writeLock = new(1, 1);
    private static MessageBoxResult? _result;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<FileTransferServiceWin> _logger;
    private readonly IViewModelFactory _viewModelFactory;
    private readonly IDialogProvider _dialogProvider;

    public FileTransferServiceWin(
        IUiDispatcher dispatcher,
        IViewModelFactory viewModelFactory,
        IDialogProvider dialogProvider,
        ILogger<FileTransferServiceWin> logger)
    {
        _dispatcher = dispatcher;
        _viewModelFactory = viewModelFactory;
        _dialogProvider = dialogProvider;
        _logger = logger;
    }

    public string GetBaseDirectory()
    {
        var programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Directory.CreateDirectory(Path.Combine(programDataPath, "RemoteControl", "Shared")).FullName;
    }

    public void OpenFileTransferWindow(IViewer viewer)
    {
        _dispatcher.Invoke(() =>
        {
            if (_fileTransferWindows.TryGetValue(viewer.ViewerConnectionId, out var window))
            {
                window.Activate();
            }
            else
            {
                var viewModel = _viewModelFactory.CreateFileTransferWindowViewModel(viewer);
                window = new FileTransferWindow()
                {
                    DataContext = viewModel
                };
                window.Closed += (sender, arg) =>
                {
                    _fileTransferWindows.Remove(viewer.ViewerConnectionId, out _);
                };
                _fileTransferWindows.AddOrUpdate(viewer.ViewerConnectionId, window, (k, v) => window);
                window.Show();
            }
        });
    }

    [SupportedOSPlatform("windows")]
    public async Task ReceiveFile(byte[] buffer, string fileName, string messageId, bool endOfFile, bool startOfFile)
    {
        try
        {
            await _writeLock.WaitAsync();

            var baseDir = GetBaseDirectory();

            SetFileOrFolderPermissions(baseDir);

            if (startOfFile)
            {
                // G3: the file name comes from the remote viewer and must never
                // escape the transfer directory. Reduce it to a bare file name
                // (strips every directory component) and fail closed on anything
                // malformed.
                if (!PathSanitizer.TrySanitizeFileName(fileName, out var safeFileName))
                {
                    _logger.LogWarning(
                        "File transfer rejected: unsafe or malformed file name received.");
                    return;
                }

                var filePath = Path.Combine(baseDir, safeFileName);

                // Defence in depth: the combined path must resolve inside the
                // transfer directory. With a sanitized name this cannot fail,
                // but the check makes the boundary explicit and testable.
                var fullBaseDir = Path.GetFullPath(baseDir)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var fullFilePath = Path.GetFullPath(filePath);
                if (!fullFilePath.StartsWith(fullBaseDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(fullFilePath, fullBaseDir, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "File transfer rejected: resolved path escapes the transfer directory.");
                    return;
                }

                if (File.Exists(filePath))
                {
                    var count = 0;
                    var ext = Path.GetExtension(safeFileName);
                    var fileWithoutExt = Path.GetFileNameWithoutExtension(safeFileName);
                    while (File.Exists(filePath))
                    {
                        filePath = Path.Combine(baseDir, $"{fileWithoutExt}-{count}{ext}");
                        count++;
                    }
                }

                File.Create(filePath).Close();
                SetFileOrFolderPermissions(filePath);
                var fs = new FileStream(filePath, FileMode.OpenOrCreate);
                _partialTransfers.AddOrUpdate(messageId, fs, (k, v) => fs);
            }

            var fileStream = _partialTransfers[messageId];

            if (buffer?.Length > 0)
            {
                await fileStream.WriteAsync(buffer);

            }

            if (endOfFile)
            {
                fileStream.Close();
                _partialTransfers.Remove(messageId, out _);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while receiving file.");
        }
        finally
        {
            _writeLock.Release();
        }

        if (endOfFile)
        {
            // We're currently in the context of an RPC call from the
            // SignalR hub.  We don't want to block it, which will prevent
            // subsequent messages from being received.
            ShowTransferComplete().Forget();
        }
    }

    public async Task UploadFile(
        FileUpload fileUpload,
        IViewer viewer,
        Action<double> progressUpdateCallback,
        CancellationToken cancelToken)
    {
        try
        {
            await viewer.SendFile(fileUpload, progressUpdateCallback, cancelToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while uploading file.");
        }
    }

    [SupportedOSPlatform("windows")]
    private static void SetFileOrFolderPermissions(string path)
    {
        FileSystemSecurity ds;

        var aclSections = AccessControlSections.Access | AccessControlSections.Group | AccessControlSections.Owner;
        if (File.Exists(path))
        {
            ds = new FileSecurity(path, aclSections);
        }
        else if (Directory.Exists(path))
        {
            ds = new DirectorySecurity(path, aclSections);
        }
        else
        {
            return;
        }

        // G3 / least privilege: instead of granting Modify to the entire
        // Builtin Users group, grant it only to the current interactive user.
        // SYSTEM, Administrators, and TrustedInstaller rights remain via
        // inherited rules from ProgramData; every other local account loses
        // direct write access to the transfer directory.
        var account = new NTAccount(Environment.UserDomainName, Environment.UserName);

        var accessAlreadySet = false;

        foreach (FileSystemAccessRule rule in ds.GetAccessRules(true, true, typeof(NTAccount)))
        {
            if (rule.IdentityReference == account &&
                rule.FileSystemRights.HasFlag(FileSystemRights.Modify) &&
                rule.AccessControlType == AccessControlType.Allow)
            {
                accessAlreadySet = true;
                break;
            }
        }

        if (!accessAlreadySet)
        {
            ds.AddAccessRule(new FileSystemAccessRule(account, FileSystemRights.Modify, AccessControlType.Allow));
            if (File.Exists(path))
            {
                new FileInfo(path).SetAccessControl((FileSecurity)ds);
            }
            else if (Directory.Exists(path))
            {
                new DirectoryInfo(path).SetAccessControl((DirectorySecurity)ds);
            }
        }
    }

    private async Task ShowTransferComplete()
    {
        // Prevent multiple dialogs from popping up.
        if (_result is null)
        {
            _result = await _dialogProvider.Show("File transfer complete.  Show folder?",
                "Transfer Complete",
                MessageBoxType.YesNo);

            if (_result == MessageBoxResult.Yes)
            {
                Process.Start("explorer.exe", GetBaseDirectory());
            }

            _result = null;
        }
    }
}
