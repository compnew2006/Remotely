namespace Remotely.Server.Auth;

/// <summary>
/// Marks an action that performs its own authorization check inside the
/// method body, because the credential it needs is neither an API key nor a
/// browser session - a device session key, a download that is allowed for
/// anonymous callers, or a legacy login that carries the credentials in the
/// request.
/// <para>
/// The attribute exists so that the decision is visible in the code.  The API
/// surface test treats an action without any of the known authorization
/// mechanisms as unprotected, and this attribute is one of them.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class InternallyAuthorizedAttribute : Attribute
{
}
