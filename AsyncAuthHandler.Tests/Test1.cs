using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AsyncAuthHandler;

namespace AsyncAuthHandler.Tests;

[TestClass]
public sealed class Test1
{
    [TestMethod]
    public void TestMethod1()
    {
    }

    [TestMethod]
    public async Task AuthenticateAsync_ReturnsTrue_WithValidCredentials()
    {
        // Arrange
        var authenticator = new Authenticator();
        var validSamid = "SMK\\u1"; // Replace with a real valid user for actual integration

        // Act
        var result = await authenticator.AuthenticateAsync(validSamid);

        // Assert
        Assert.IsTrue(result, "Authentication should succeed for valid credentials.");
    }

    [TestMethod]
    public async Task AuthenticateAsync_MultipleParallelUsers_AllReturnTrue()
    {
        // Arrange
        int N = 10; // You can adjust N as needed
        var authenticator = new Authenticator();
        var tasks = new Task<bool>[N];

        // Act
        for (int i = 0; i < N; i++)
        {
            string user = $"SMK\\u{i + 1}"; // Users: SMK\u1, SMK\u2, ...
            tasks[i] = authenticator.AuthenticateAsync(user);
        }
        var results = await Task.WhenAll(tasks);

        // Assert
        for (int i = 0; i < N; i++)
        {
            Assert.IsTrue(results[i], $"Authentication should succeed for user SMK\\u{i + 1}.");
        }
    }
}
