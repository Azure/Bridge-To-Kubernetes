// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

export class ThenableUtility {
    // Converts a Thenable to a native Promise.
    public static async ToPromise<T>(thenable: Thenable<T>): Promise<T> {
        return new Promise<T>((resolve, reject): void => {
            const onFulfilled = (value: T): void => {
                resolve(value);
            };

            const onRejected = (reason: any): void => {
                reject(reason);
            };

            thenable.then(onFulfilled, onRejected);
        });
    }
}