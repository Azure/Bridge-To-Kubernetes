// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

export class ObjectUtility {
    // Performs a deep-copy on the object.
    public static deepCopyObject(originalObject: any): any {
        if (typeof originalObject !== `object`) {
            return originalObject;
        }

        // Arrays are detected as "object", but we need to treat them specifically.
        if (Array.isArray(originalObject)) {
            return Array.from(originalObject);
        }

        const copyObject = {};
        for (const key of Object.keys(originalObject)) {
            copyObject[key] = ObjectUtility.deepCopyObject(originalObject[key]);
        }

        return copyObject;
    }
}