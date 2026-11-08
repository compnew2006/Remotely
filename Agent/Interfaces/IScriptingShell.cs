namespace Remotely.Agent.Interfaces;
public interface IScriptingShell : System.IDisposable
{
    bool IsBusy { get; }
    bool IsDisposed { get; }
}
