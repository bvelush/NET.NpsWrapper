#ifndef RADUTIL_H
#define RADUTIL_H
#pragma once

#include <authif.h>
#include <windows.h>

#ifdef __cplusplus
extern "C" {
#endif

    LPVOID
        WINAPI
        RadiusAlloc(
            SIZE_T dwBytes
        );

    VOID
        WINAPI
        RadiusFree(
            LPVOID lpMem
        );

#define RADIUS_ATTR_NOT_FOUND ((DWORD)-1)

    /* Returns the index of the first attribute with the desired type or
     * RADIUS_ATTR_NOT_FOUND if no such attribute exists. */
    DWORD
        WINAPI
        RadiusFindFirstIndex(
            PRADIUS_ATTRIBUTE_ARRAY pAttrs,
            DWORD dwAttrType
        );

    /* Returns the first attribute with the desired type or NULL if no such
     * attribute exists. */
    const RADIUS_ATTRIBUTE*
        WINAPI
        RadiusFindFirstAttribute(
            PRADIUS_ATTRIBUTE_ARRAY pAttrs,
            DWORD dwAttrType
        );

    /* Replaces the first attribute of the specified type or appends the new
     * attribute to the end of the request if no such attribute exists. */
    DWORD
        WINAPI
        RadiusReplaceFirstAttribute(
            PRADIUS_ATTRIBUTE_ARRAY pAttrs,
            const RADIUS_ATTRIBUTE* pSrc
        );


#ifdef __cplusplus
}
#endif
#endif // RADUTIL_H
