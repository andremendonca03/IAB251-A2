using Xunit;
using AeroLink.Models;

namespace AeroLink
{
    /// <summary>Verifies the handling acknowledgement rule for User Story 57.</summary>
    public class BaggageTests
    {
        /// <summary>Allows standard baggage without an explicit handling acknowledgement.</summary>
        [Fact]
        public void CanConfirmLoading_ReturnsTrue_ForStandardBaggageWithoutAcknowledgment()
        {
            var handler = new SpecialBaggageHandler();
            string handlingType = "Standard";
            bool isAcknowledged = false;

            bool canLoad = handler.CanConfirmLoading(handlingType, isAcknowledged);

            Assert.True(canLoad, "Standard baggage should be allowed to load without requiring special acknowledgment.");
        }

        /// <summary>Blocks special baggage until its instructions have been acknowledged.</summary>
        [Fact]
        public void CanConfirmLoading_ReturnsFalse_WhenSpecialBaggageIsNotAcknowledged()
        {
            var handler = new SpecialBaggageHandler();
            string handlingType = "Fragile";
            bool isAcknowledged = false;

            bool canLoad = handler.CanConfirmLoading(handlingType, isAcknowledged);

            Assert.False(canLoad, "Loading confirmation must be blocked when special handling instructions have not been acknowledged.");
        }

        /// <summary>Allows special baggage once its handling instructions are acknowledged.</summary>
        [Fact]
        public void CanConfirmLoading_ReturnsTrue_WhenSpecialBaggageIsAcknowledged()
        {
            var handler = new SpecialBaggageHandler();
            string handlingType = "Fragile";
            bool isAcknowledged = true;

            bool canLoad = handler.CanConfirmLoading(handlingType, isAcknowledged);

            Assert.True(canLoad, "Loading confirmation should be enabled once special handling instructions are acknowledged.");
        }
    }
}