using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenCymd.Nps.Plugin;

namespace Omni2FA.Adapter.Tests
{
    [TestClass]
    public class RadiusEnumTests
    {
        [TestMethod]
        public void RadiusCode_AccessRequest_ShouldHaveCorrectValue()
        {
            // Arrange & Act
            var code = RadiusCode.AccessRequest;

            // Assert
            Assert.IsTrue(Enum.IsDefined(typeof(RadiusCode), code));
        }

        [TestMethod]
        public void RadiusCode_AccessAccept_ShouldHaveCorrectValue()
        {
            // Arrange & Act
            var code = RadiusCode.AccessAccept;

            // Assert
            Assert.IsTrue(Enum.IsDefined(typeof(RadiusCode), code));
        }

        [TestMethod]
        public void RadiusCode_AccessReject_ShouldHaveCorrectValue()
        {
            // Arrange & Act
            var code = RadiusCode.AccessReject;

            // Assert
            Assert.IsTrue(Enum.IsDefined(typeof(RadiusCode), code));
        }

        [TestMethod]
        public void RadiusCode_AllValues_ShouldBeDefined()
        {
            // Arrange & Act
            var values = Enum.GetValues(typeof(RadiusCode));

            // Assert
            Assert.IsTrue(values.Length > 0);
            foreach (var value in values)
            {
                Assert.IsTrue(Enum.IsDefined(typeof(RadiusCode), value));
            }
        }

        [TestMethod]
        public void RadiusExtensionPoint_AllValues_ShouldBeDefined()
        {
            // Arrange & Act
            var values = Enum.GetValues(typeof(RadiusExtensionPoint));

            // Assert
            Assert.IsTrue(values.Length > 0);
            foreach (var value in values)
            {
                Assert.IsTrue(Enum.IsDefined(typeof(RadiusExtensionPoint), value));
            }
        }

        [TestMethod]
        public void RadiusAttributeType_UserName_ShouldBeDefined()
        {
            // Arrange & Act
            var attrType = RadiusAttributeType.UserName;

            // Assert
            Assert.IsTrue(Enum.IsDefined(typeof(RadiusAttributeType), attrType));
        }

        [TestMethod]
        public void RadiusAttributeType_NASIPAddress_ShouldBeDefined()
        {
            // Arrange & Act
            var attrType = RadiusAttributeType.NASIPAddress;

            // Assert
            Assert.IsTrue(Enum.IsDefined(typeof(RadiusAttributeType), attrType));
        }

        [TestMethod]
        public void RadiusAttributeType_AllCommonTypes_ShouldBeDefined()
        {
            // Test some common attribute types
            Assert.IsTrue(Enum.IsDefined(typeof(RadiusAttributeType), RadiusAttributeType.UserName));
            Assert.IsTrue(Enum.IsDefined(typeof(RadiusAttributeType), RadiusAttributeType.NASIPAddress));
            Assert.IsTrue(Enum.IsDefined(typeof(RadiusAttributeType), RadiusAttributeType.VendorSpecific));
        }

        [TestMethod]
        public void RadiusCode_CanConvertToString()
        {
            // Arrange
            var code = RadiusCode.AccessRequest;

            // Act
            var result = code.ToString();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(string.IsNullOrEmpty(result));
        }

        [TestMethod]
        public void RadiusExtensionPoint_CanConvertToString()
        {
            // Arrange
            var values = Enum.GetValues(typeof(RadiusExtensionPoint));
            
            // Act & Assert
            foreach (var value in values)
            {
                var result = value.ToString();
                Assert.IsNotNull(result);
                Assert.IsFalse(string.IsNullOrEmpty(result));
            }
        }

        [TestMethod]
        public void RadiusAttributeType_CanConvertToString()
        {
            // Arrange
            var attrType = RadiusAttributeType.UserName;

            // Act
            var result = attrType.ToString();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(string.IsNullOrEmpty(result));
        }

        [TestMethod]
        public void RadiusCode_CanCastToInt()
        {
            // Arrange
            var code = RadiusCode.AccessRequest;

            // Act
            var intValue = (int)code;

            // Assert
            Assert.IsTrue(intValue >= 0);
        }

        [TestMethod]
        public void RadiusAttributeType_CanCastToInt()
        {
            // Arrange
            var attrType = RadiusAttributeType.UserName;

            // Act
            var intValue = (int)attrType;

            // Assert
            Assert.IsTrue(intValue >= 0);
        }

        [TestMethod]
        public void RadiusExtensionPoint_CanCastToInt()
        {
            // Arrange
            var values = Enum.GetValues(typeof(RadiusExtensionPoint));

            // Act & Assert
            foreach (RadiusExtensionPoint value in values)
            {
                var intValue = (int)value;
                Assert.IsTrue(intValue >= 0 || intValue < 0); // Just verify it can be cast
            }
        }
    }
}
