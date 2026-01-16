namespace Scalextric;

/// <summary>
/// Throttle profile curve types for different throttle response characteristics.
/// </summary>
public enum ThrottleProfileType
{
    /// <summary>Proportional response - input maps linearly to output.</summary>
    Linear,

    /// <summary>Gentle at low input, aggressive at high - better control at low speeds.</summary>
    Exponential,

    /// <summary>Distinct power bands - beginner-friendly with clear speed zones.</summary>
    Stepped
}
