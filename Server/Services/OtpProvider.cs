using Remotely.Shared.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace Remotely.Server.Services;

public interface IOtpProvider
{
    /// <summary>
    /// Issues a one-time password for a device.  It is valid for one minute
    /// and for one use.
    /// </summary>
    string GetOtp(string deviceId);

    /// <summary>
    /// Consumes the one-time password and returns an admission ticket.
    /// <para>
    /// The password is good for exactly one use: the page that redeems it
    /// gets a ticket, and the ticket is what the browser presents when it
    /// opens the hub connection.  A password that stays valid for its whole
    /// minute is a credential that can be replayed from a proxy log or from
    /// the history of the browser that opened the link.
    /// </para>
    /// </summary>
    /// <returns>The ticket, or null when the password is unknown or was used already.</returns>
    string? RedeemOtp(string otp);

    /// <summary>
    /// Whether the admission ticket is still valid.  A ticket is not consumed
    /// by this call, because the browser opens more than one connection when
    /// it reconnects.
    /// </summary>
    bool HasAdmission(string? ticket);
}

public class OtpProvider : IOtpProvider
{
    /// <summary>
    /// How long a one-time password may be redeemed.
    /// </summary>
    public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long an admission ticket stays valid.  It is long enough for the
    /// browser to reconnect after a network drop, and short enough that a
    /// leaked cookie is not a lasting credential.
    /// </summary>
    public static readonly TimeSpan AdmissionLifetime = TimeSpan.FromMinutes(15);

    private const string AdmissionKeyPrefix = "admission:";

    private static readonly MemoryCache _otpCache = new(new MemoryCacheOptions());

    public string GetOtp(string deviceId)
    {
        var otp = RandomGenerator.GenerateString(16);
        _otpCache.Set(otp, deviceId, OtpLifetime);
        return otp;
    }

    public string? RedeemOtp(string otp)
    {
        if (string.IsNullOrWhiteSpace(otp))
        {
            return null;
        }

        if (!_otpCache.TryGetValue(otp, out _))
        {
            return null;
        }

        // One use only.  The entry goes before the ticket is issued, so two
        // requests that arrive together cannot both redeem the same password.
        _otpCache.Remove(otp);

        var ticket = RandomGenerator.GenerateString(32);
        _otpCache.Set(AdmissionKeyPrefix + ticket, true, AdmissionLifetime);

        return ticket;
    }

    public bool HasAdmission(string? ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket))
        {
            return false;
        }

        return _otpCache.TryGetValue(AdmissionKeyPrefix + ticket, out _);
    }
}
