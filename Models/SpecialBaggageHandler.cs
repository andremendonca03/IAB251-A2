namespace AeroLink.Models;

public class SpecialBaggageHandler
{
    public bool CanConfirmLoading(string handlingType, bool isAcknowledged)
    {
        if (string.Equals(handlingType, "Standard", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return isAcknowledged;
    }
}