using Xunit;
using AeroLink.Models;

namespace AeroLink
{
    /// Unit tests for User Story 57:
    public class BaggageTests
    {
        [Fact]
        public void CanConfirmLoading_ReturnsTrue_ForStandardBaggageWithoutAcknowledgment()
        {
            // Set up standard baggage where no special acknowledgment is needed
            var handler = new SpecialBaggageHandler();
            string handlingType = "Standard";
            bool isAcknowledged = false;

            // Evaluate loading permission
            bool canLoad = handler.CanConfirmLoading(handlingType, isAcknowledged);

            // Standard bags should be allowed to load freely
            Assert.True(canLoad, "Standard baggage should be allowed to load without requiring special acknowledgment.");
        }

        [Fact]
        public void CanConfirmLoading_ReturnsFalse_WhenSpecialBaggageIsNotAcknowledged()
        {
            // Arrange: Set up a special bag (e.g., Fragile) that has NOT been acknowledged
            var handler = new SpecialBaggageHandler();
            string handlingType = "Fragile";
            bool isAcknowledged = false;

            // Act: Evaluate loading permission
            bool canLoad = handler.CanConfirmLoading(handlingType, isAcknowledged);

            // Assert: Loading confirmation must be blocked until instructions are acknowledged
            Assert.False(canLoad, "Loading confirmation must be blocked when special handling instructions have not been acknowledged.");
        }

        [Fact]
        public void CanConfirmLoading_ReturnsTrue_WhenSpecialBaggageIsAcknowledged()
        {
            // Arrange: Set up a special bag (e.g., Fragile) where the employee HAS acknowledged instructions
            var handler = new SpecialBaggageHandler();
            string handlingType = "Fragile";
            bool isAcknowledged = true;

            // Act: Evaluate loading permission
            bool canLoad = handler.CanConfirmLoading(handlingType, isAcknowledged);

            // Assert: Loading confirmation should be enabled once acknowledged
            Assert.True(canLoad, "Loading confirmation should be enabled once special handling instructions are acknowledged.");
        }
    }
}