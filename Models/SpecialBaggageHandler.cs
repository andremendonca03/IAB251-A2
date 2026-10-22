namespace AeroLink.Models;

/// <summary>Determines whether handling instructions need to be acknowledged before loading.</summary>
public class SpecialBaggageHandler
{
    /// <summary>Allows standard baggage immediately; other handling types require acknowledgement.</summary>
    public bool CanConfirmLoading(string handlingType, bool isAcknowledged)
    {
        if (string.Equals(handlingType, "Standard", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return isAcknowledged;
    }
}