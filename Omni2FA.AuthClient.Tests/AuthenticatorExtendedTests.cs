using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Omni2FA.AuthClient;

namespace Omni2FA.AuthClient.Tests
{
    /// <summary>
    /// Tests for LogLevel enum in AuthClient
    /// </summary>
    [TestClass]
    public class LogLevelTests
    {
        [TestMethod]
        public void LogLevel_AllValues_ShouldBeDefined()
        {
            // Arrange & Act
            var values = Enum.GetValues(typeof(LogLevel));

            // Assert
            Assert.IsTrue(values.Length > 0, "LogLevel enum should have at least one value");
        }

        [TestMethod]
        public void LogLevel_Trace_ShouldBeDefined()
        {
            // Act
            var level = LogLevel.Trace;

            // Assert
            Assert.IsTrue(Enum.IsDefined(typeof(LogLevel), level));
        }

        [TestMethod]
        public void LogLevel_Information_ShouldBeDefined()
        {
            // Act
            var level = LogLevel.Information;

            // Assert
            Assert.IsTrue(Enum.IsDefined(typeof(LogLevel), level));
        }

        [TestMethod]
        public void LogLevel_Warning_ShouldBeDefined()
        {
            // Act
            var level = LogLevel.Warning;

            // Assert
            Assert.IsTrue(Enum.IsDefined(typeof(LogLevel), level));
        }

        [TestMethod]
        public void LogLevel_Error_ShouldBeDefined()
        {
            // Act
            var level = LogLevel.Error;

            // Assert
            Assert.IsTrue(Enum.IsDefined(typeof(LogLevel), level));
        }

        [TestMethod]
        public void LogLevel_CanConvertToString()
        {
            // Arrange
            var levels = new[] { LogLevel.Trace, LogLevel.Information, LogLevel.Warning, LogLevel.Error };

            // Act & Assert
            foreach (var level in levels)
            {
                string result = level.ToString();
                Assert.IsNotNull(result, $"ToString() should return non-null for {level}");
                Assert.IsFalse(string.IsNullOrEmpty(result), $"ToString() should return non-empty string for {level}");
            }
        }

        [TestMethod]
        public void LogLevel_CanCastToInt()
        {
            // Arrange
            var levels = new[] { LogLevel.Trace, LogLevel.Information, LogLevel.Warning, LogLevel.Error };

            // Act & Assert
            foreach (var level in levels)
            {
                int intValue = (int)level;
                Assert.IsTrue(intValue >= 0, $"Integer value should be non-negative for {level}");
            }
        }

        [TestMethod]
        public void LogLevel_Values_ShouldBeDistinct()
        {
            // Arrange
            var trace = (int)LogLevel.Trace;
            var information = (int)LogLevel.Information;
            var warning = (int)LogLevel.Warning;
            var error = (int)LogLevel.Error;

            // Assert - All values should be distinct
            Assert.AreNotEqual(trace, information);
            Assert.AreNotEqual(trace, warning);
            Assert.AreNotEqual(trace, error);
            Assert.AreNotEqual(information, warning);
            Assert.AreNotEqual(information, error);
            Assert.AreNotEqual(warning, error);
        }

        [TestMethod]
        public void LogLevel_ShouldHaveExpectedCount()
        {
            // Arrange & Act
            var values = Enum.GetValues(typeof(LogLevel));

            // Assert - Should have exactly 4 levels
            Assert.AreEqual(4, values.Length, "LogLevel should have exactly 4 values: Trace, Information, Warning, Error");
        }
    }

    /// <summary>
    /// Additional edge case tests for Authenticator
    /// </summary>
    [TestClass]
    public class AuthenticatorEdgeCaseTests
    {
        [TestMethod]
        public void Authenticator_MultipleInstances_ShouldBeIndependent()
        {
            // Arrange & Act
            var auth1 = new Authenticator();
            var auth2 = new Authenticator();
            var auth3 = new Authenticator();

            // Assert
            Assert.IsNotNull(auth1);
            Assert.IsNotNull(auth2);
            Assert.IsNotNull(auth3);
            Assert.AreNotSame(auth1, auth2);
            Assert.AreNotSame(auth2, auth3);
            Assert.AreNotSame(auth1, auth3);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task AuthenticateAsync_WithWhitespaceUsername_ShouldHandleGracefully()
        {
            // Arrange
            var authenticator = new Authenticator();
            var testSamid = "   "; // Only whitespace

            // Act
            var result = await authenticator.AuthenticateAsync(testSamid);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task AuthenticateAsync_WithNewlineInUsername_ShouldHandleGracefully()
        {
            // Arrange
            var authenticator = new Authenticator();
            var testSamid = "user\nname";

            // Act
            var result = await authenticator.AuthenticateAsync(testSamid);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task AuthenticateAsync_WithTabInUsername_ShouldHandleGracefully()
        {
            // Arrange
            var authenticator = new Authenticator();
            var testSamid = "user\tname";

            // Act
            var result = await authenticator.AuthenticateAsync(testSamid);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task AuthenticateAsync_WithBackslashOnly_ShouldHandleGracefully()
        {
            // Arrange
            var authenticator = new Authenticator();
            var testSamid = "\\";

            // Act
            var result = await authenticator.AuthenticateAsync(testSamid);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task AuthenticateAsync_WithAtSymbolOnly_ShouldHandleGracefully()
        {
            // Arrange
            var authenticator = new Authenticator();
            var testSamid = "@";

            // Act
            var result = await authenticator.AuthenticateAsync(testSamid);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task AuthenticateAsync_WithMixedCaseUsername_ShouldHandleGracefully()
        {
            // Arrange
            var authenticator = new Authenticator();
            var testSamid = "TeSt.UsEr@DoMaIn.CoM";

            // Act
            var result = await authenticator.AuthenticateAsync(testSamid);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task AuthenticateAsync_WithNumericUsername_ShouldHandleGracefully()
        {
            // Arrange
            var authenticator = new Authenticator();
            var testSamid = "123456789";

            // Act
            var result = await authenticator.AuthenticateAsync(testSamid);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task AuthenticateAsync_WithLeadingWhitespace_ShouldHandleGracefully()
        {
            // Arrange
            var authenticator = new Authenticator();
            var testSamid = "   testuser";

            // Act
            var result = await authenticator.AuthenticateAsync(testSamid);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task AuthenticateAsync_WithTrailingWhitespace_ShouldHandleGracefully()
        {
            // Arrange
            var authenticator = new Authenticator();
            var testSamid = "testuser   ";

            // Act
            var result = await authenticator.AuthenticateAsync(testSamid);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task AuthenticateAsync_WithSurroundingWhitespace_ShouldHandleGracefully()
        {
            // Arrange
            var authenticator = new Authenticator();
            var testSamid = "   testuser   ";

            // Act
            var result = await authenticator.AuthenticateAsync(testSamid);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Constructor_CalledMultipleTimes_ShouldSucceed()
        {
            // Arrange & Act - Create multiple instances in succession
            for (int i = 0; i < 10; i++)
            {
                var auth = new Authenticator();
                Assert.IsNotNull(auth);
            }

            // Assert - If we get here, all constructions succeeded
            Assert.IsTrue(true);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task AuthenticateAsync_WithJsonSpecialCharacters_ShouldHandleGracefully()
        {
            // Arrange
            var authenticator = new Authenticator();
            var testSamid = "user\"{[]}\\";

            // Act
            var result = await authenticator.AuthenticateAsync(testSamid);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task AuthenticateAsync_WithSingleQuote_ShouldHandleGracefully()
        {
            // Arrange
            var authenticator = new Authenticator();
            var testSamid = "user'name";

            // Act
            var result = await authenticator.AuthenticateAsync(testSamid);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task AuthenticateAsync_WithDoubleQuote_ShouldHandleGracefully()
        {
            // Arrange
            var authenticator = new Authenticator();
            var testSamid = "user\"name";

            // Act
            var result = await authenticator.AuthenticateAsync(testSamid);

            // Assert
            Assert.IsFalse(result);
        }
    }
}
