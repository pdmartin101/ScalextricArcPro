using Scalextric;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Application-level helper for creating throttle profiles based on ThrottleProfileType.
/// Wraps the library's ScalextricProtocol.ThrottleProfile with app-specific enum mapping.
/// </summary>
public static class ThrottleProfileHelper
{
    /// <summary>
    /// Creates a throttle curve for the specified profile type.
    /// </summary>
    /// <param name="profileType">The type of throttle response curve.</param>
    /// <returns>96-byte array with throttle values.</returns>
    public static byte[] CreateCurve(ThrottleProfileType profileType) => profileType switch
    {
        ThrottleProfileType.Linear => ScalextricProtocol.ThrottleProfile.CreateLinearCurve(),
        ThrottleProfileType.Exponential => ScalextricProtocol.ThrottleProfile.CreateExponentialCurve(),
        ThrottleProfileType.Stepped => ScalextricProtocol.ThrottleProfile.CreateSteppedCurve(),
        _ => ScalextricProtocol.ThrottleProfile.CreateLinearCurve()
    };

    /// <summary>
    /// Creates blocks for the specified throttle profile type ready to write.
    /// </summary>
    /// <param name="profileType">The type of throttle response curve.</param>
    /// <returns>Array of 6 blocks, each 17 bytes.</returns>
    public static byte[][] CreateBlocks(ThrottleProfileType profileType)
    {
        return ScalextricProtocol.ThrottleProfile.GetAllBlocks(CreateCurve(profileType));
    }
}
