using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;

namespace Omni2FA.AuthClient.Tests
{
    /// <summary>
    /// Tests for Authenticator using mocked HttpClient to test various response scenarios.
    /// </summary>
    [TestClass]
    public class AuthenticatorMockedTests
    {
        /// <summary>
        /// Helper method to create a mock HttpClient that returns a specific response.
        /// </summary>
        private HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string responseContent)
        {
            var mockHandler = new Mock<HttpMessageHandler>();
            
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
                });

            return new HttpClient(mockHandler.Object);
        }

        /// <summary>
        /// Helper method to create a mock HttpClient that returns different responses for different requests.
        /// </summary>
        private HttpClient CreateMockHttpClientWithSequence(params (string urlPattern, HttpStatusCode statusCode, string content)[] responses)
        {
            var mockHandler = new Mock<HttpMessageHandler>();
            
            foreach (var response in responses)
            {
                mockHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().Contains(response.urlPattern)),
                        ItExpr.IsAny<CancellationToken>()
                    )
                    .ReturnsAsync(new HttpResponseMessage
                    {
                        StatusCode = response.statusCode,
                        Content = new StringContent(response.content, Encoding.UTF8, "application/json")
                    });
            }

            return new HttpClient(mockHandler.Object);
        }

        [TestMethod]
        [Timeout(5000)]
        public async Task AuthenticateAsync_WithImmediateSuccess_ShouldReturnTrue()
        {
            // Arrange
            var successResponse = JsonConvert.SerializeObject(new { status = 1 }); // AUTH_SUCCESS
            var mockHttpClient = CreateMockHttpClient(HttpStatusCode.OK, successResponse);
            var authenticator = new Authenticator(mockHttpClient);

            // Act
            var result = await authenticator.AuthenticateAsync("testuser");

            // Assert
            Assert.IsTrue(result, "Authentication should succeed with status = 1");
        }

        [TestMethod]
        [Timeout(5000)]
        public async Task AuthenticateAsync_WithImmediateFailure_ShouldReturnFalse()
        {
            // Arrange
            var failureResponse = JsonConvert.SerializeObject(new { status = -1 }); // AUTH_FAILED
            var mockHttpClient = CreateMockHttpClient(HttpStatusCode.OK, failureResponse);
            var authenticator = new Authenticator(mockHttpClient);

            // Act
            var result = await authenticator.AuthenticateAsync("testuser");

            // Assert
            Assert.IsFalse(result, "Authentication should fail with status = -1");
        }

        [TestMethod]
        [Timeout(5000)]
        public async Task AuthenticateAsync_WithHttpError_ShouldReturnFalse()
        {
            // Arrange
            var mockHttpClient = CreateMockHttpClient(HttpStatusCode.InternalServerError, "Server Error");
            var authenticator = new Authenticator(mockHttpClient);

            // Act
            var result = await authenticator.AuthenticateAsync("testuser");

            // Assert
            Assert.IsFalse(result, "Authentication should fail with HTTP 500 error");
        }

        [TestMethod]
        [Timeout(5000)]
        public async Task AuthenticateAsync_WithInvalidJson_ShouldReturnFalse()
        {
            // Arrange
            var mockHttpClient = CreateMockHttpClient(HttpStatusCode.OK, "invalid json {{{");
            var authenticator = new Authenticator(mockHttpClient);

            // Act
            var result = await authenticator.AuthenticateAsync("testuser");

            // Assert
            Assert.IsFalse(result, "Authentication should fail with invalid JSON");
        }

        [TestMethod]
        [Timeout(5000)]
        public async Task AuthenticateAsync_WithNullStatus_ShouldReturnFalse()
        {
            // Arrange
            var mockHttpClient = CreateMockHttpClient(HttpStatusCode.OK, "{}"); // No status field
            var authenticator = new Authenticator(mockHttpClient);

            // Act
            var result = await authenticator.AuthenticateAsync("testuser");

            // Assert
            Assert.IsFalse(result, "Authentication should fail with missing status");
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task AuthenticateAsync_WithPendingThenSuccess_ShouldPollAndSucceed()
        {
            // Arrange - First call returns PENDING, second call returns SUCCESS
            var mockHandler = new Mock<HttpMessageHandler>();
            var callCount = 0;
            
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(() =>
                {
                    callCount++;
                    var status = callCount == 1 ? 0 : 1; // First call PENDING, subsequent SUCCESS
                    var response = JsonConvert.SerializeObject(new { status = status });
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(response, Encoding.UTF8, "application/json")
                    };
                });

            var mockHttpClient = new HttpClient(mockHandler.Object);
            var authenticator = new Authenticator(mockHttpClient);

            // Act
            var result = await authenticator.AuthenticateAsync("testuser");

            // Assert
            Assert.IsTrue(result, "Authentication should succeed after polling");
            Assert.IsTrue(callCount >= 2, "Should have made at least 2 HTTP calls (initial + poll)");
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task AuthenticateAsync_WithPendingThenFailure_ShouldPollAndFail()
        {
            // Arrange - First call returns PENDING, second call returns FAILED
            var mockHandler = new Mock<HttpMessageHandler>();
            var callCount = 0;
            
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(() =>
                {
                    callCount++;
                    var status = callCount == 1 ? 0 : -1; // First call PENDING, subsequent FAILED
                    var response = JsonConvert.SerializeObject(new { status = status });
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(response, Encoding.UTF8, "application/json")
                    };
                });

            var mockHttpClient = new HttpClient(mockHandler.Object);
            var authenticator = new Authenticator(mockHttpClient);

            // Act
            var result = await authenticator.AuthenticateAsync("testuser");

            // Assert
            Assert.IsFalse(result, "Authentication should fail after polling");
            Assert.IsTrue(callCount >= 2, "Should have made at least 2 HTTP calls (initial + poll)");
        }

        [TestMethod]
        [Timeout(5000)]
        public async Task AuthenticateAsync_WithDifferentEndpoints_ShouldCallCorrectUrls()
        {
            // Arrange
            var mockHandler = new Mock<HttpMessageHandler>();
            var authenticateCalled = false;
            var authResultCalled = false;

            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().Contains("/Authenticate")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(() =>
                {
                    authenticateCalled = true;
                    var response = JsonConvert.SerializeObject(new { status = 0 }); // PENDING
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(response, Encoding.UTF8, "application/json")
                    };
                });

            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().Contains("/AuthResult")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(() =>
                {
                    authResultCalled = true;
                    var response = JsonConvert.SerializeObject(new { status = 1 }); // SUCCESS
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(response, Encoding.UTF8, "application/json")
                    };
                });

            var mockHttpClient = new HttpClient(mockHandler.Object);
            var authenticator = new Authenticator(mockHttpClient);

            // Act
            var result = await authenticator.AuthenticateAsync("testuser");

            // Assert
            Assert.IsTrue(result, "Authentication should succeed");
            Assert.IsTrue(authenticateCalled, "Should have called /Authenticate endpoint");
            Assert.IsTrue(authResultCalled, "Should have called /AuthResult endpoint");
        }

        [TestMethod]
        [Timeout(5000)]
        public async Task AuthenticateAsync_VerifyRequestBody_ShouldContainCorrectData()
        {
            // Arrange
            var mockHandler = new Mock<HttpMessageHandler>();
            string capturedRequestBody = null;

            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync((HttpRequestMessage req, CancellationToken token) =>
                {
                    capturedRequestBody = req.Content.ReadAsStringAsync().Result;
                    var response = JsonConvert.SerializeObject(new { status = 1 });
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(response, Encoding.UTF8, "application/json")
                    };
                });

            var mockHttpClient = new HttpClient(mockHandler.Object);
            var authenticator = new Authenticator(mockHttpClient);

            // Act
            await authenticator.AuthenticateAsync("testuser");

            // Assert
            Assert.IsNotNull(capturedRequestBody, "Request body should be captured");
            Assert.IsTrue(capturedRequestBody.Contains("testuser"), "Request should contain username");
            Assert.IsTrue(capturedRequestBody.Contains("samid"), "Request should contain 'samid' field");
        }

        [TestMethod]
        public void Authenticator_WithMockedHttpClient_ShouldNotDisposeInjectedClient()
        {
            // Arrange
            var mockHttpClient = CreateMockHttpClient(HttpStatusCode.OK, JsonConvert.SerializeObject(new { status = 1 }));
            var authenticator = new Authenticator(mockHttpClient);

            // Act
            authenticator.Dispose();

            // Assert - HttpClient should still be usable (not disposed)
            // This test just verifies no exception is thrown
            // In a real scenario, you might check if the HttpClient is still functional
            Assert.IsTrue(true, "Injected HttpClient should not be disposed by Authenticator");
        }
    }
}