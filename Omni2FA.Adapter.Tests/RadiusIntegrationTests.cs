using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenCymd.Nps.Plugin;

namespace Omni2FA.Adapter.Tests
{
    [TestClass]
    public class RadiusIntegrationTests
    {
        [TestMethod]
        public void CreateMultipleAttributes_WithDifferentTypes_ShouldWorkCorrectly()
        {
            // Arrange & Act
            var stringAttr = new RadiusAttribute(RadiusAttributeType.UserName, "testuser");
            var intAttr = new RadiusAttribute(RadiusAttributeType.NASPort, 12345);
            var ipv4Attr = new RadiusAttribute(RadiusAttributeType.NASIPAddress, IPAddress.Parse("192.168.1.1"));
            var byteAttr = new RadiusAttribute(10, new byte[] { 0x01, 0x02, 0x03 });

            // Assert
            Assert.IsNotNull(stringAttr);
            Assert.IsNotNull(intAttr);
            Assert.IsNotNull(ipv4Attr);
            Assert.IsNotNull(byteAttr);
            
            Assert.AreEqual("testuser", stringAttr.Value);
            // When an int is passed, it's stored and returned as int
            Assert.AreEqual(12345, intAttr.Value);
            Assert.AreEqual(IPAddress.Parse("192.168.1.1"), ipv4Attr.Value);
        }

        [TestMethod]
        public void RadiusAttribute_WithVSA_ShouldIntegrateCorrectly()
        {
            // Arrange
            uint vendorId = 12345;
            byte vendorType = 1;
            byte[] vsaData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var vsa = new VendorSpecificAttribute(vendorId, vendorType, vsaData);

            // Act
            var attribute = new RadiusAttribute(RadiusAttributeType.VendorSpecific, vsa);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual((int)RadiusAttributeType.VendorSpecific, attribute.AttributeId);
        }

        [TestMethod]
        public void CreateAttributeCollection_ShouldWorkCorrectly()
        {
            // Arrange
            var attributes = new List<RadiusAttribute>
            {
                new RadiusAttribute(RadiusAttributeType.UserName, "user1"),
                new RadiusAttribute(RadiusAttributeType.UserName, "user2"),
                new RadiusAttribute(RadiusAttributeType.NASIPAddress, IPAddress.Parse("10.0.0.1")),
                new RadiusAttribute(RadiusAttributeType.NASPort, 8080)
            };

            // Assert
            Assert.AreEqual(4, attributes.Count);
            Assert.AreEqual("user1", attributes[0].Value);
            Assert.AreEqual("user2", attributes[1].Value);
        }

        [TestMethod]
        public void RadiusAttribute_WithSpecialCharactersInUsername_ShouldHandleCorrectly()
        {
            // Arrange
            var specialChars = new[]
            {
                "user@domain.com",
                "DOMAIN\\user",
                "user name with spaces",
                "user-with-dash",
                "user_with_underscore",
                "user.with.dots",
                "user'with'quotes"
            };

            // Act & Assert
            foreach (var username in specialChars)
            {
                var attribute = new RadiusAttribute(RadiusAttributeType.UserName, username);
                Assert.IsNotNull(attribute);
                Assert.AreEqual(username, attribute.Value);
            }
        }

        [TestMethod]
        public void RadiusAttribute_WithDifferentIPFormats_ShouldHandleCorrectly()
        {
            // Arrange
            var ipAddresses = new[]
            {
                IPAddress.Parse("127.0.0.1"),
                IPAddress.Parse("192.168.1.1"),
                IPAddress.Parse("10.0.0.0"),
                IPAddress.Parse("255.255.255.255"),
                IPAddress.Parse("::1"),
                IPAddress.Parse("2001:db8::1")
            };

            // Act & Assert
            foreach (var ip in ipAddresses)
            {
                var attribute = new RadiusAttribute(RadiusAttributeType.NASIPAddress, ip);
                Assert.IsNotNull(attribute);
                Assert.AreEqual(ip, attribute.Value);
            }
        }

        [TestMethod]
        public void RadiusAttribute_WithBoundaryValues_ShouldHandleCorrectly()
        {
            // Arrange & Act & Assert
            
            // Minimum values - using int literal returns int
            var minInt = new RadiusAttribute(1, 0);
            Assert.AreEqual(0, minInt.Value);

            // Maximum values - using uint returns uint
            var maxInt = new RadiusAttribute(1, uint.MaxValue);
            Assert.AreEqual(uint.MaxValue, maxInt.Value);

            // Empty string
            var emptyString = new RadiusAttribute(1, string.Empty);
            Assert.AreEqual(string.Empty, emptyString.Value);

            // Single character
            var singleChar = new RadiusAttribute(1, "a");
            Assert.AreEqual("a", singleChar.Value);
        }

        [TestMethod]
        public void RadiusAttributeType_EnumValues_ShouldBeConsistent()
        {
            // Arrange & Act
            var userNameValue = (int)RadiusAttributeType.UserName;
            var nasIpValue = (int)RadiusAttributeType.NASIPAddress;

            // Assert - These should be consistent with RADIUS RFC values
            Assert.IsTrue(userNameValue > 0);
            Assert.IsTrue(nasIpValue > 0);
            Assert.AreNotEqual(userNameValue, nasIpValue);
        }

        [TestMethod]
        public void CreateVSA_WithDifferentVendors_ShouldDistinguish()
        {
            // Arrange
            byte[] data = new byte[] { 0x01, 0x02 };
            var vsa1 = new VendorSpecificAttribute(311, 1, data); // Microsoft
            var vsa2 = new VendorSpecificAttribute(9, 1, data);   // Cisco
            var vsa3 = new VendorSpecificAttribute(3902, 1, data); // Another vendor

            // Assert
            Assert.AreNotEqual(vsa1.VendorId, vsa2.VendorId);
            Assert.AreNotEqual(vsa2.VendorId, vsa3.VendorId);
            Assert.AreNotEqual(vsa1.VendorId, vsa3.VendorId);
        }

        [TestMethod]
        public void RadiusAttribute_Stress_CreateManyAttributes()
        {
            // Arrange
            var attributes = new List<RadiusAttribute>();

            // Act
            for (int i = 0; i < 1000; i++)
            {
                attributes.Add(new RadiusAttribute(1, $"user{i}"));
            }

            // Assert
            Assert.AreEqual(1000, attributes.Count);
            Assert.AreEqual("user0", attributes[0].Value);
            Assert.AreEqual("user999", attributes[999].Value);
        }

        [TestMethod]
        public void RadiusAttribute_WithLongUsernames_ShouldHandle()
        {
            // Test with progressively longer usernames
            for (int length = 10; length <= 500; length += 50)
            {
                var username = new string('a', length);
                var attribute = new RadiusAttribute(RadiusAttributeType.UserName, username);
                Assert.AreEqual(username, attribute.Value);
            }
        }

        [TestMethod]
        public void RadiusAttribute_WithByteArrays_DifferentSizes()
        {
            // Test different byte array sizes
            var sizes = new[] { 1, 10, 50, 100, 200, 255 };
            
            foreach (var size in sizes)
            {
                var data = new byte[size];
                for (int i = 0; i < size; i++)
                {
                    data[i] = (byte)(i % 256);
                }
                
                var attribute = new RadiusAttribute(10, data);
                Assert.IsNotNull(attribute);
                Assert.AreEqual(size, ((byte[])attribute.Value).Length);
            }
        }
    }
}
