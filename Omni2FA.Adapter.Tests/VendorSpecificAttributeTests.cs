using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenCymd.Nps.Plugin;

namespace Omni2FA.Adapter.Tests
{
    [TestClass]
    public class VendorSpecificAttributeTests
    {
        [TestMethod]
        public void Constructor_WithValidParameters_ShouldCreateVSA()
        {
            // Arrange
            uint vendorId = 12345;
            byte vendorType = 1;
            byte[] data = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            // Act
            var vsa = new VendorSpecificAttribute(vendorId, vendorType, data);

            // Assert
            Assert.IsNotNull(vsa);
            Assert.AreEqual(vendorId, vsa.VendorId);
            Assert.AreEqual(vendorType, vsa.VendorType);
            CollectionAssert.AreEqual(data, vsa.Data);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_WithNullData_ShouldThrowArgumentNullException()
        {
            // Arrange
            uint vendorId = 12345;
            byte vendorType = 1;
            byte[] data = null;

            // Act
            var vsa = new VendorSpecificAttribute(vendorId, vendorType, data);

            // Assert - Exception expected
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_WithEmptyData_ShouldThrowArgumentNullException()
        {
            // Arrange
            uint vendorId = 12345;
            byte vendorType = 1;
            byte[] data = new byte[0];

            // Act
            var vsa = new VendorSpecificAttribute(vendorId, vendorType, data);

            // Assert - Exception expected
        }

        [TestMethod]
        public void Constructor_WithSingleByteData_ShouldCreateVSA()
        {
            // Arrange
            uint vendorId = 12345;
            byte vendorType = 1;
            byte[] data = new byte[] { 0xFF };

            // Act
            var vsa = new VendorSpecificAttribute(vendorId, vendorType, data);

            // Assert
            Assert.IsNotNull(vsa);
            Assert.AreEqual(vendorId, vsa.VendorId);
            Assert.AreEqual(vendorType, vsa.VendorType);
            CollectionAssert.AreEqual(data, vsa.Data);
        }

        [TestMethod]
        public void Constructor_WithMaximumData_ShouldCreateVSA()
        {
            // Arrange
            uint vendorId = 12345;
            byte vendorType = 1;
            byte[] data = new byte[byte.MaxValue - 2]; // Maximum allowed size
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 256);
            }

            // Act
            var vsa = new VendorSpecificAttribute(vendorId, vendorType, data);

            // Assert
            Assert.IsNotNull(vsa);
            Assert.AreEqual(vendorId, vsa.VendorId);
            Assert.AreEqual(vendorType, vsa.VendorType);
            Assert.AreEqual(data.Length, vsa.Data.Length);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Constructor_WithTooLargeData_ShouldThrowArgumentException()
        {
            // Arrange
            uint vendorId = 12345;
            byte vendorType = 1;
            byte[] data = new byte[byte.MaxValue - 1]; // One byte too large

            // Act
            var vsa = new VendorSpecificAttribute(vendorId, vendorType, data);

            // Assert - Exception expected
        }

        [TestMethod]
        public void Constructor_WithZeroVendorId_ShouldCreateVSA()
        {
            // Arrange
            uint vendorId = 0;
            byte vendorType = 1;
            byte[] data = new byte[] { 0x01, 0x02 };

            // Act
            var vsa = new VendorSpecificAttribute(vendorId, vendorType, data);

            // Assert
            Assert.IsNotNull(vsa);
            Assert.AreEqual(vendorId, vsa.VendorId);
        }

        [TestMethod]
        public void Constructor_WithMaxVendorId_ShouldCreateVSA()
        {
            // Arrange
            uint vendorId = uint.MaxValue;
            byte vendorType = 1;
            byte[] data = new byte[] { 0x01, 0x02 };

            // Act
            var vsa = new VendorSpecificAttribute(vendorId, vendorType, data);

            // Assert
            Assert.IsNotNull(vsa);
            Assert.AreEqual(vendorId, vsa.VendorId);
        }

        [TestMethod]
        public void Constructor_WithZeroVendorType_ShouldCreateVSA()
        {
            // Arrange
            uint vendorId = 12345;
            byte vendorType = 0;
            byte[] data = new byte[] { 0x01, 0x02 };

            // Act
            var vsa = new VendorSpecificAttribute(vendorId, vendorType, data);

            // Assert
            Assert.IsNotNull(vsa);
            Assert.AreEqual(vendorType, vsa.VendorType);
        }

        [TestMethod]
        public void Constructor_WithMaxVendorType_ShouldCreateVSA()
        {
            // Arrange
            uint vendorId = 12345;
            byte vendorType = byte.MaxValue;
            byte[] data = new byte[] { 0x01, 0x02 };

            // Act
            var vsa = new VendorSpecificAttribute(vendorId, vendorType, data);

            // Assert
            Assert.IsNotNull(vsa);
            Assert.AreEqual(vendorType, vsa.VendorType);
        }

        [TestMethod]
        public void ImplicitConversion_ToByteArray_ShouldReturnValidData()
        {
            // Arrange
            uint vendorId = 12345;
            byte vendorType = 1;
            byte[] data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var vsa = new VendorSpecificAttribute(vendorId, vendorType, data);

            // Act
            byte[] result = vsa;

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length >= data.Length + 6); // 4 bytes vendor ID + 2 bytes header + data
        }

        [TestMethod]
        public void ToString_ShouldReturnFormattedString()
        {
            // Arrange
            uint vendorId = 12345;
            byte vendorType = 1;
            byte[] data = new byte[] { 0x01, 0x02, 0x03 };
            var vsa = new VendorSpecificAttribute(vendorId, vendorType, data);

            // Act
            string result = vsa.ToString();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("VSA:"));
            Assert.IsTrue(result.Contains("ID="));
            Assert.IsTrue(result.Contains("Type="));
            Assert.IsTrue(result.Contains("Data="));
            Assert.IsTrue(result.Contains("12345"));
            Assert.IsTrue(result.Contains("1"));
        }

        [TestMethod]
        public void ToString_WithLargeData_ShouldReturnFormattedString()
        {
            // Arrange
            uint vendorId = 999999;
            byte vendorType = 255;
            byte[] data = new byte[100];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 256);
            }
            var vsa = new VendorSpecificAttribute(vendorId, vendorType, data);

            // Act
            string result = vsa.ToString();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("VSA:"));
            Assert.IsTrue(result.Contains("999999"));
            Assert.IsTrue(result.Contains("255"));
        }

        [TestMethod]
        public void Data_Property_ShouldReturnReferenceToInternalData()
        {
            // Arrange
            uint vendorId = 12345;
            byte vendorType = 1;
            byte[] originalData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var vsa = new VendorSpecificAttribute(vendorId, vendorType, originalData);

            // Act
            byte[] retrievedData = vsa.Data;
            retrievedData[0] = 0xFF; // Modify retrieved data

            // Assert
            // The Data property returns a reference to the internal array, not a copy
            // So modifying retrievedData affects the internal state
            Assert.AreEqual(0xFF, vsa.Data[0], "Data property returns reference to internal array");
        }
    }
}
