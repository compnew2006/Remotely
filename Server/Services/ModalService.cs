using Microsoft.AspNetCore.Components;
using Remotely.Server.Models;

namespace Remotely.Server.Services;

public interface IModalService
{
    event EventHandler ModalShown;
    List<ModalButton> Buttons { get; }
    string[]? Body { get; }
    RenderFragment? RenderBody { get; }
    string Title { get; }
    string? Size { get; }
    void Hide();
    Task ShowModal(string title, string[] body, ModalButton[]? buttons = null, string? size = null);
    Task ShowModal(string title, RenderFragment body, ModalButton[]? buttons = null, string? size = null);
}

public class ModalService : IModalService
{
    private readonly SemaphoreSlim _modalLock = new(1, 1);

    public event EventHandler? ModalShown;

    public List<ModalButton> Buttons { get; } = new List<ModalButton>();
    public string[]? Body { get; private set; }
    public RenderFragment? RenderBody { get; private set; }
    public bool ShowInput { get; private set; }
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// The Bootstrap size modifier of the modal dialog (for example "lg" or
    /// "xl").  Null keeps the default width.
    /// </summary>
    public string? Size { get; private set; }

    public async Task ShowModal(string title, string[] body, ModalButton[]? buttons = null, string? size = null)
    {
        try
        {
            await _modalLock.WaitAsync();
            Title = title;
            Body = body;
            RenderBody = null;
            Size = size;
            Buttons.Clear();
            if (buttons is not null)
            {
                Buttons.AddRange(buttons);
            }
            ModalShown?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _modalLock.Release();
        }
    }

    public async Task ShowModal(string title, RenderFragment body, ModalButton[]? buttons = null, string? size = null)
    {
        try
        {
            await _modalLock.WaitAsync();
            Title = title;
            RenderBody = body;
            Body = null;
            Size = size;
            Buttons.Clear();
            if (buttons is not null)
            {
                Buttons.AddRange(buttons);
            }
            ModalShown?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _modalLock.Release();
        }
    }

    /// <summary>
    /// Clears the modal's content.  This disposes the content component, which
    /// stops anything it was doing in the background (timers, script polling).
    /// </summary>
    public void Hide()
    {
        Title = string.Empty;
        Body = null;
        RenderBody = null;
        Size = null;
        Buttons.Clear();
    }
}
