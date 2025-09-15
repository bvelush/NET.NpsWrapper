using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AsyncAuthHandler;

namespace AsyncAuthHandler.Tests
{
    [TestClass]
    public class AuthenticatorIntegrationTests
    {
        [TestMethod]
        public async Task AuthenticateAsync_ReturnsFalse_WhenServiceIsUnavailable()
        {
            // Arrange
            var authenticator = new Authenticator();
            var testSamid = "testuser";

            // Act
            var result = await authenticator.AuthenticateAsync(testSamid);

            // Assert
            Assert.IsFalse(result, "Authentication should fail when service is unavailable.");
        }
    }
}
