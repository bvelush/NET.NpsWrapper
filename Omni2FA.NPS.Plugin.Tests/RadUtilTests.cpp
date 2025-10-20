// --------------------------------------------------------------------------------------------------------------------
// <copyright>
//   Copyright 2024 Omni2FA
//   
//   Unit tests for radutil.cpp functions
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

#include <gtest/gtest.h>
#include <windows.h>
#include "radutil.h"
#include <vector>
#include <memory>

// Mock RADIUS_ATTRIBUTE structure that we can actually modify
struct TestRadiusAttribute {
    DWORD dwAttrType;
    RADIUS_DATA_TYPE fDataType;
    DWORD cbDataLength;
    BYTE buffer[256];  // Local buffer instead of const pointer
};

// Mock RADIUS_ATTRIBUTE_ARRAY for testing  
// Must match the exact memory layout of RADIUS_ATTRIBUTE_ARRAY from authif.h
class MockRadiusAttributeArray {
private:
    // The actual RADIUS_ATTRIBUTE_ARRAY structure layout:
    DWORD cbSize;  // Size of structure
    DWORD (WINAPI *Add_ptr)(RADIUS_ATTRIBUTE_ARRAY*, const RADIUS_ATTRIBUTE*);
    const RADIUS_ATTRIBUTE* (WINAPI *AttributeAt_ptr)(const RADIUS_ATTRIBUTE_ARRAY*, DWORD);
    DWORD (WINAPI *GetSize_ptr)(const RADIUS_ATTRIBUTE_ARRAY*);
    DWORD (WINAPI *InsertAt_ptr)(RADIUS_ATTRIBUTE_ARRAY*, DWORD, const RADIUS_ATTRIBUTE*);
    DWORD (WINAPI *RemoveAt_ptr)(RADIUS_ATTRIBUTE_ARRAY*, DWORD);
    DWORD (WINAPI *SetAt_ptr)(RADIUS_ATTRIBUTE_ARRAY*, DWORD, const RADIUS_ATTRIBUTE*);

public:
    std::vector<TestRadiusAttribute> attributes;
    mutable std::vector<RADIUS_ATTRIBUTE> convertedAttributes;

    MockRadiusAttributeArray() {
        // Initialize function pointers - order matters!
        cbSize = sizeof(RADIUS_ATTRIBUTE_ARRAY);
        Add_ptr = Add_Impl;
        AttributeAt_ptr = AttributeAt_Impl;
        GetSize_ptr = GetSize_Impl;
        InsertAt_ptr = nullptr;  // Not implemented
        RemoveAt_ptr = nullptr;  // Not implemented
        SetAt_ptr = SetAt_Impl;
    }

    static DWORD WINAPI GetSize_Impl(const RADIUS_ATTRIBUTE_ARRAY* pThis) {
        // Skip past the RADIUS_ATTRIBUTE_ARRAY structure part to get to our data
        auto* mock = reinterpret_cast<const MockRadiusAttributeArray*>(pThis);
        return static_cast<DWORD>(mock->attributes.size());
    }

    static const RADIUS_ATTRIBUTE* WINAPI AttributeAt_Impl(const RADIUS_ATTRIBUTE_ARRAY* pThis, DWORD dwIndex) {
        auto* mock = reinterpret_cast<const MockRadiusAttributeArray*>(pThis);
        if (dwIndex < mock->attributes.size()) {
            // Ensure we have space in convertedAttributes
            if (mock->convertedAttributes.size() <= dwIndex) {
                mock->convertedAttributes.resize(mock->attributes.size());
            }
            
            // Build RADIUS_ATTRIBUTE pointing to our buffer
            mock->convertedAttributes[dwIndex].dwAttrType = mock->attributes[dwIndex].dwAttrType;
            mock->convertedAttributes[dwIndex].fDataType = mock->attributes[dwIndex].fDataType;
            mock->convertedAttributes[dwIndex].cbDataLength = mock->attributes[dwIndex].cbDataLength;
            // Point to the actual buffer in our attributes vector
            mock->convertedAttributes[dwIndex].lpValue = const_cast<BYTE*>(mock->attributes[dwIndex].buffer);
            
            return &mock->convertedAttributes[dwIndex];
        }
        return nullptr;
    }

    static DWORD WINAPI SetAt_Impl(RADIUS_ATTRIBUTE_ARRAY* pThis, DWORD dwIndex, const RADIUS_ATTRIBUTE* pAttr) {
        auto* mock = reinterpret_cast<MockRadiusAttributeArray*>(pThis);
        if (dwIndex < mock->attributes.size() && pAttr != nullptr) {
            mock->attributes[dwIndex].dwAttrType = pAttr->dwAttrType;
            mock->attributes[dwIndex].fDataType = pAttr->fDataType;
            mock->attributes[dwIndex].cbDataLength = pAttr->cbDataLength;
            if (pAttr->cbDataLength > 0 && pAttr->lpValue != nullptr && pAttr->cbDataLength <= 256) {
                memcpy(mock->attributes[dwIndex].buffer, pAttr->lpValue, pAttr->cbDataLength);
            }
            return NO_ERROR;
        }
        return ERROR_INVALID_PARAMETER;
    }

    static DWORD WINAPI Add_Impl(RADIUS_ATTRIBUTE_ARRAY* pThis, const RADIUS_ATTRIBUTE* pAttr) {
        auto* mock = reinterpret_cast<MockRadiusAttributeArray*>(pThis);
        if (pAttr != nullptr) {
            TestRadiusAttribute attr = {};
            attr.dwAttrType = pAttr->dwAttrType;
            attr.fDataType = pAttr->fDataType;
            attr.cbDataLength = pAttr->cbDataLength;
            if (pAttr->cbDataLength > 0 && pAttr->lpValue != nullptr && pAttr->cbDataLength <= 256) {
                memcpy(attr.buffer, pAttr->lpValue, pAttr->cbDataLength);
            }
            mock->attributes.push_back(attr);
            return NO_ERROR;
        }
        return ERROR_INVALID_PARAMETER;
    }

    RADIUS_ATTRIBUTE_ARRAY* ToRadiusArray() {
        return reinterpret_cast<RADIUS_ATTRIBUTE_ARRAY*>(this);
    }
};

// Test fixture for RadUtil tests
class RadUtilTest : public ::testing::Test {
protected:
    std::unique_ptr<MockRadiusAttributeArray> mockArray;
    RADIUS_ATTRIBUTE_ARRAY* radiusArray;

    void SetUp() override {
        mockArray = std::make_unique<MockRadiusAttributeArray>();
        radiusArray = mockArray->ToRadiusArray();
    }

    void TearDown() override {
        radiusArray = nullptr;
        mockArray.reset();
    }

    void AddAttribute(DWORD type, const BYTE* value, DWORD length) {
        TestRadiusAttribute attr = {};
        attr.dwAttrType = type;
        attr.fDataType = rdtUnknown;
        attr.cbDataLength = length;
        if (value != nullptr && length > 0 && length <= sizeof(attr.buffer)) {
            memcpy(attr.buffer, value, length);
        }
        mockArray->attributes.push_back(attr);
    }
};

// ============================================================================
// RadiusAlloc / RadiusFree Tests
// ============================================================================

TEST(RadiusAllocTest, AllocatesMemorySuccessfully) {
    LPVOID ptr = RadiusAlloc(1024);
    ASSERT_NE(ptr, nullptr);
    RadiusFree(ptr);
}

TEST(RadiusAllocTest, AllocatesZeroBytes) {
    LPVOID ptr = RadiusAlloc(0);
    // HeapAlloc with 0 bytes may return NULL or a valid pointer
    // This is implementation-dependent, so we just test it doesn't crash
    if (ptr != nullptr) {
        RadiusFree(ptr);
    }
    SUCCEED();
}

TEST(RadiusAllocTest, AllocatesLargeBlock) {
    SIZE_T largeSize = 1024 * 1024; // 1 MB
    LPVOID ptr = RadiusAlloc(largeSize);
    ASSERT_NE(ptr, nullptr);
    
    // Write to memory to ensure it's actually allocated
    memset(ptr, 0xFF, largeSize);
    
    RadiusFree(ptr);
}

TEST(RadiusFreeTest, FreesNullPointer) {
    // Should not crash
    RadiusFree(nullptr);
    SUCCEED();
}

// ============================================================================
// RadiusFindFirstIndex Tests
// ============================================================================

TEST_F(RadUtilTest, FindFirstIndex_ReturnsNotFoundForNullArray) {
    DWORD result = RadiusFindFirstIndex(nullptr, 1);
    EXPECT_EQ(result, RADIUS_ATTR_NOT_FOUND);
}

TEST_F(RadUtilTest, FindFirstIndex_ReturnsNotFoundForEmptyArray) {
    DWORD result = RadiusFindFirstIndex(radiusArray, 1);
    EXPECT_EQ(result, RADIUS_ATTR_NOT_FOUND);
}

TEST_F(RadUtilTest, FindFirstIndex_FindsAttributeAtBeginning) {
    AddAttribute(1, nullptr, 0);
    AddAttribute(2, nullptr, 0);
    AddAttribute(3, nullptr, 0);

    DWORD result = RadiusFindFirstIndex(radiusArray, 1);
    EXPECT_EQ(result, 0);
}

TEST_F(RadUtilTest, FindFirstIndex_FindsAttributeInMiddle) {
    AddAttribute(1, nullptr, 0);
    AddAttribute(2, nullptr, 0);
    AddAttribute(3, nullptr, 0);

    DWORD result = RadiusFindFirstIndex(radiusArray, 2);
    EXPECT_EQ(result, 1);
}

TEST_F(RadUtilTest, FindFirstIndex_FindsAttributeAtEnd) {
    AddAttribute(1, nullptr, 0);
    AddAttribute(2, nullptr, 0);
    AddAttribute(3, nullptr, 0);

    DWORD result = RadiusFindFirstIndex(radiusArray, 3);
    EXPECT_EQ(result, 2);
}

TEST_F(RadUtilTest, FindFirstIndex_ReturnsNotFoundForNonExistentAttribute) {
    AddAttribute(1, nullptr, 0);
    AddAttribute(2, nullptr, 0);
    AddAttribute(3, nullptr, 0);

    DWORD result = RadiusFindFirstIndex(radiusArray, 99);
    EXPECT_EQ(result, RADIUS_ATTR_NOT_FOUND);
}

TEST_F(RadUtilTest, FindFirstIndex_FindsFirstOccurrenceWhenDuplicatesExist) {
    AddAttribute(1, nullptr, 0);
    AddAttribute(2, nullptr, 0);
    AddAttribute(2, nullptr, 0); // Duplicate
    AddAttribute(3, nullptr, 0);

    DWORD result = RadiusFindFirstIndex(radiusArray, 2);
    EXPECT_EQ(result, 1); // Should find the first occurrence
}

// ============================================================================
// RadiusFindFirstAttribute Tests
// ============================================================================

TEST_F(RadUtilTest, FindFirstAttribute_ReturnsNullForNullArray) {
    const RADIUS_ATTRIBUTE* result = RadiusFindFirstAttribute(nullptr, 1);
    EXPECT_EQ(result, nullptr);
}

TEST_F(RadUtilTest, FindFirstAttribute_ReturnsNullForEmptyArray) {
    const RADIUS_ATTRIBUTE* result = RadiusFindFirstAttribute(radiusArray, 1);
    EXPECT_EQ(result, nullptr);
}

TEST_F(RadUtilTest, FindFirstAttribute_FindsExistingAttribute) {
    BYTE data[] = { 0x01, 0x02, 0x03 };
    AddAttribute(1, data, sizeof(data));
    AddAttribute(2, nullptr, 0);

    const RADIUS_ATTRIBUTE* result = RadiusFindFirstAttribute(radiusArray, 1);
    ASSERT_NE(result, nullptr);
    EXPECT_EQ(result->dwAttrType, 1);
    EXPECT_EQ(result->cbDataLength, sizeof(data));
}

TEST_F(RadUtilTest, FindFirstAttribute_ReturnsNullForNonExistentAttribute) {
    AddAttribute(1, nullptr, 0);
    AddAttribute(2, nullptr, 0);

    const RADIUS_ATTRIBUTE* result = RadiusFindFirstAttribute(radiusArray, 99);
    EXPECT_EQ(result, nullptr);
}

TEST_F(RadUtilTest, FindFirstAttribute_ReturnsCorrectAttributeWithData) {
    BYTE data1[] = { 0xAA, 0xBB };
    BYTE data2[] = { 0xCC, 0xDD, 0xEE };
    
    AddAttribute(1, data1, sizeof(data1));
    AddAttribute(2, data2, sizeof(data2));

    const RADIUS_ATTRIBUTE* result = RadiusFindFirstAttribute(radiusArray, 2);
    ASSERT_NE(result, nullptr);
    EXPECT_EQ(result->dwAttrType, 2);
    EXPECT_EQ(result->cbDataLength, sizeof(data2));
    
    // Verify the data content - with proper null checks
    ASSERT_NE(result->lpValue, nullptr);
    ASSERT_EQ(result->cbDataLength, sizeof(data2));
    
    // Compare byte by byte to avoid any memcmp issues
    bool dataMatches = true;
    for (size_t i = 0; i < sizeof(data2); ++i) {
        if (result->lpValue[i] != data2[i]) {
            dataMatches = false;
            break;
        }
    }
    EXPECT_TRUE(dataMatches);
}

// ============================================================================
// RadiusReplaceFirstAttribute Tests
// ============================================================================

TEST_F(RadUtilTest, ReplaceFirstAttribute_ReturnsErrorForNullArray) {
    BYTE buffer[256] = {};
    RADIUS_ATTRIBUTE attr = {};
    attr.dwAttrType = 1;
    attr.lpValue = buffer;
    
    DWORD result = RadiusReplaceFirstAttribute(nullptr, &attr);
    EXPECT_EQ(result, ERROR_INVALID_PARAMETER);
}

TEST_F(RadUtilTest, ReplaceFirstAttribute_ReturnsErrorForNullAttribute) {
    DWORD result = RadiusReplaceFirstAttribute(radiusArray, nullptr);
    EXPECT_EQ(result, ERROR_INVALID_PARAMETER);
}

TEST_F(RadUtilTest, ReplaceFirstAttribute_AddsNewAttributeWhenNotExists) {
    BYTE data[] = { 0x01, 0x02 };
    BYTE buffer[256];
    memcpy(buffer, data, sizeof(data));
    
    RADIUS_ATTRIBUTE attr = {};
    attr.dwAttrType = 1;
    attr.cbDataLength = sizeof(data);
    attr.lpValue = buffer;

    DWORD result = RadiusReplaceFirstAttribute(radiusArray, &attr);
    EXPECT_EQ(result, NO_ERROR);
    
    // Verify attribute was added
    EXPECT_EQ(mockArray->attributes.size(), 1);
    EXPECT_EQ(mockArray->attributes[0].dwAttrType, 1);
    EXPECT_EQ(mockArray->attributes[0].cbDataLength, sizeof(data));
}

TEST_F(RadUtilTest, ReplaceFirstAttribute_ReplacesExistingAttribute) {
    // Add initial attribute
    BYTE initialData[] = { 0xAA, 0xBB };
    AddAttribute(1, initialData, sizeof(initialData));
    AddAttribute(2, nullptr, 0);

    // Replace attribute type 1 with new data
    BYTE newData[] = { 0xCC, 0xDD, 0xEE };
    BYTE buffer[256];
    memcpy(buffer, newData, sizeof(newData));
    
    RADIUS_ATTRIBUTE newAttr = {};
    newAttr.dwAttrType = 1;
    newAttr.cbDataLength = sizeof(newData);
    newAttr.lpValue = buffer;

    DWORD result = RadiusReplaceFirstAttribute(radiusArray, &newAttr);
    EXPECT_EQ(result, NO_ERROR);
    
    // Verify attribute was replaced (not added)
    EXPECT_EQ(mockArray->attributes.size(), 2);
    EXPECT_EQ(mockArray->attributes[0].dwAttrType, 1);
    EXPECT_EQ(mockArray->attributes[0].cbDataLength, sizeof(newData));
    EXPECT_EQ(memcmp(mockArray->attributes[0].buffer, newData, sizeof(newData)), 0);
}

TEST_F(RadUtilTest, ReplaceFirstAttribute_ReplacesFirstOccurrenceWhenDuplicates) {
    // Add duplicate attributes
    BYTE data1[] = { 0x11 };
    BYTE data2[] = { 0x22 };
    AddAttribute(1, data1, sizeof(data1));
    AddAttribute(1, data2, sizeof(data2)); // Duplicate type

    // Replace - should replace first occurrence
    BYTE newData[] = { 0xFF };
    BYTE buffer[256];
    memcpy(buffer, newData, sizeof(newData));
    
    RADIUS_ATTRIBUTE newAttr = {};
    newAttr.dwAttrType = 1;
    newAttr.cbDataLength = sizeof(newData);
    newAttr.lpValue = buffer;

    DWORD result = RadiusReplaceFirstAttribute(radiusArray, &newAttr);
    EXPECT_EQ(result, NO_ERROR);
    
    // First attribute should be replaced
    EXPECT_EQ(mockArray->attributes[0].buffer[0], 0xFF);
    // Second attribute should remain unchanged
    EXPECT_EQ(mockArray->attributes[1].buffer[0], 0x22);
}

TEST_F(RadUtilTest, ReplaceFirstAttribute_AppendsToEndWhenNotFound) {
    AddAttribute(1, nullptr, 0);
    AddAttribute(2, nullptr, 0);

    BYTE data[] = { 0xAA };
    BYTE buffer[256];
    memcpy(buffer, data, sizeof(data));
    
    RADIUS_ATTRIBUTE newAttr = {};
    newAttr.dwAttrType = 3;
    newAttr.cbDataLength = sizeof(data);
    newAttr.lpValue = buffer;

    DWORD result = RadiusReplaceFirstAttribute(radiusArray, &newAttr);
    EXPECT_EQ(result, NO_ERROR);
    
    // Should be added at the end
    EXPECT_EQ(mockArray->attributes.size(), 3);
    EXPECT_EQ(mockArray->attributes[2].dwAttrType, 3);
}

// ============================================================================
// Main entry point
// ============================================================================

int main(int argc, char** argv) {
    ::testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}
