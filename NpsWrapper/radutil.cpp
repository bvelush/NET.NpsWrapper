#include "pch.h"
#include <windows.h>
#include "radutil.h"

LPVOID WINAPI RadiusAlloc(SIZE_T dwBytes)
{
    return HeapAlloc(GetProcessHeap(), 0, dwBytes);
}
VOID WINAPI RadiusFree(LPVOID lpMem)
{
    HeapFree(GetProcessHeap(), 0, lpMem);
}
DWORD WINAPI RadiusFindFirstIndex(PRADIUS_ATTRIBUTE_ARRAY pAttrs,DWORD dwAttrType)
{
    DWORD dwIndex, dwSize;
    const RADIUS_ATTRIBUTE* pAttr;
    if (pAttrs == NULL)
    {
        return RADIUS_ATTR_NOT_FOUND;
    }
    /* Get the number of attributes in the array */
    dwSize = pAttrs->GetSize(pAttrs);
    /* Iterate through the array ... */
    for (dwIndex = 0; dwIndex < dwSize; ++dwIndex)
    {
        /* ... looking for the first attribute that matches the type. */
        pAttr = pAttrs->AttributeAt(pAttrs, dwIndex);
        if (pAttr->dwAttrType == dwAttrType)
        {
            return dwIndex;
        }
    }
    return RADIUS_ATTR_NOT_FOUND;
}
const RADIUS_ATTRIBUTE* WINAPI RadiusFindFirstAttribute(PRADIUS_ATTRIBUTE_ARRAY pAttrs,DWORD dwAttrType)
{
    DWORD dwIndex;
    dwIndex = RadiusFindFirstIndex(pAttrs, dwAttrType);
    if (dwIndex != RADIUS_ATTR_NOT_FOUND)
    {
        return pAttrs->AttributeAt(pAttrs, dwIndex);
    }
    else
    {
        return NULL;
    }
}
DWORD WINAPI RadiusReplaceFirstAttribute(PRADIUS_ATTRIBUTE_ARRAY pAttrs,const RADIUS_ATTRIBUTE* pSrc)
{
    DWORD dwIndex;
    if ((pAttrs == NULL) || (pSrc == NULL))
    {
        return ERROR_INVALID_PARAMETER;
    }
    dwIndex = RadiusFindFirstIndex(pAttrs, pSrc->dwAttrType);
    if (dwIndex != RADIUS_ATTR_NOT_FOUND)
    {
        /* It already exists, so overwrite the existing attribute. */
        return pAttrs->SetAt(pAttrs, dwIndex, pSrc);
    }
    else
    {
        /* It doesn't exist, so add it to the end of the array. */
        return pAttrs->Add(pAttrs, pSrc);
    }
}