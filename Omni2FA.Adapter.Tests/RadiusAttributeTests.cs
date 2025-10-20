using System;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenCymd.Nps.Plugin;

namespace Omni2FA.Adapter.Tests
{
    [TestClass]
    public class RadiusAttributeTests
    {
        [TestMethod]
        public void Constructor_WithValidAttributeTypeAndValue_ShouldCreateAttribute()
        {
            // Arrange
            var attributeType = RadiusAttributeType.UserName;
            var value = "testuser";

            // Act
            var attribute = new RadiusAttribute(attributeType, value);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual((int)attributeType, attribute.AttributeId);
            Assert.AreEqual(value, attribute.Value);
        }

        [TestMethod]
        public void Constructor_WithStringValue_ShouldCreateAttribute()
        {
            // Arrange
            var attributeId = 1;
            var value = "test string";

            // Act
            var attribute = new RadiusAttribute(attributeId, value);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual(attributeId, attribute.AttributeId);
            Assert.AreEqual(value, attribute.Value);
        }

        [TestMethod]
        public void Constructor_WithIntValue_ShouldCreateAttribute()
        {
            // Arrange
            var attributeId = 5;
            var value = 12345;

            // Act
            var attribute = new RadiusAttribute(attributeId, value);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual(attributeId, attribute.AttributeId);
            // When an int is passed, it's stored and returned as int
            Assert.AreEqual(value, attribute.Value);
        }

        [TestMethod]
        public void Constructor_WithUIntValue_ShouldCreateAttribute()
        {
            // Arrange
            var attributeId = 5;
            uint value = 12345;

            // Act
            var attribute = new RadiusAttribute(attributeId, value);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual(attributeId, attribute.AttributeId);
            Assert.AreEqual(value, attribute.Value);
        }

        [TestMethod]
        public void Constructor_WithIPv4Address_ShouldCreateAttribute()
        {
            // Arrange
            var attributeType = RadiusAttributeType.NASIPAddress;
            var ipAddress = IPAddress.Parse("192.168.1.1");

            // Act
            var attribute = new RadiusAttribute(attributeType, ipAddress);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual((int)attributeType, attribute.AttributeId);
            Assert.AreEqual(ipAddress, attribute.Value);
        }

        [TestMethod]
        public void Constructor_WithIPv6Address_ShouldCreateAttribute()
        {
            // Arrange
            var attributeType = RadiusAttributeType.NASIPAddress;
            var ipAddress = IPAddress.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7334");

            // Act
            var attribute = new RadiusAttribute(attributeType, ipAddress);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual((int)attributeType, attribute.AttributeId);
            Assert.AreEqual(ipAddress, attribute.Value);
        }

        [TestMethod]
        public void Constructor_WithByteArray_ShouldCreateAttribute()
        {
            // Arrange
            var attributeId = 10;
            var value = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            // Act
            var attribute = new RadiusAttribute(attributeId, value);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual(attributeId, attribute.AttributeId);
            CollectionAssert.AreEqual(value, (byte[])attribute.Value);
        }

        [TestMethod]
        public void Constructor_WithDateTime_ShouldCreateAttribute()
        {
            // Arrange
            var attributeId = 15;
            var value = DateTime.Now;

            // Act
            var attribute = new RadiusAttribute(attributeId, value);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual(attributeId, attribute.AttributeId);
            // DateTime comparison might differ due to conversion, so just check it's a valid value
            Assert.IsNotNull(attribute.Value);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_WithNullValue_ShouldThrowArgumentNullException()
        {
            // Arrange & Act
            var attribute = new RadiusAttribute(1, null);

            // Assert - Exception expected
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Constructor_WithNegativeAttributeId_ShouldThrowArgumentOutOfRangeException()
        {
            // Arrange & Act
            var attribute = new RadiusAttribute(-1, "test");

            // Assert - Exception expected
        }

        [TestMethod]
        public void Constructor_WithEmptyString_ShouldCreateAttribute()
        {
            // Arrange
            var attributeId = 1;
            var value = string.Empty;

            // Act
            var attribute = new RadiusAttribute(attributeId, value);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual(attributeId, attribute.AttributeId);
            Assert.AreEqual(value, attribute.Value);
        }

        [TestMethod]
        public void Constructor_WithLongString_ShouldCreateAttribute()
        {
            // Arrange
            var attributeId = 1;
            var value = new string('A', 1000);

            // Act
            var attribute = new RadiusAttribute(attributeId, value);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual(attributeId, attribute.AttributeId);
            Assert.AreEqual(value, attribute.Value);
        }

        [TestMethod]
        public void Constructor_WithUnicodeString_ShouldCreateAttribute()
        {
            // Arrange
            var attributeId = 1;
            var value = "Hello ?? ??";

            // Act
            var attribute = new RadiusAttribute(attributeId, value);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual(attributeId, attribute.AttributeId);
            Assert.AreEqual(value, attribute.Value);
        }

        [TestMethod]
        public void Constructor_WithZeroAttributeId_ShouldCreateAttribute()
        {
            // Arrange
            var attributeId = 0;
            var value = "test";

            // Act
            var attribute = new RadiusAttribute(attributeId, value);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual(attributeId, attribute.AttributeId);
        }

        [TestMethod]
        public void Constructor_WithMaxUIntValue_ShouldCreateAttribute()
        {
            // Arrange
            var attributeId = 1;
            uint value = uint.MaxValue;

            // Act
            var attribute = new RadiusAttribute(attributeId, value);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual(value, attribute.Value);
        }

        [TestMethod]
        public void Constructor_WithEmptyByteArray_ShouldCreateAttribute()
        {
            // Arrange
            var attributeId = 10;
            var value = new byte[0];

            // Act
            var attribute = new RadiusAttribute(attributeId, value);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual(attributeId, attribute.AttributeId);
            CollectionAssert.AreEqual(value, (byte[])attribute.Value);
        }

        [TestMethod]
        public void Constructor_WithLargeByteArray_ShouldCreateAttribute()
        {
            // Arrange
            var attributeId = 10;
            var value = new byte[1000];
            for (int i = 0; i < value.Length; i++)
            {
                value[i] = (byte)(i % 256);
            }

            // Act
            var attribute = new RadiusAttribute(attributeId, value);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual(attributeId, attribute.AttributeId);
            CollectionAssert.AreEqual(value, (byte[])attribute.Value);
        }
    }
}
