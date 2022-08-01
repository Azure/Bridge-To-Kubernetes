// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

export class RetryUtility {
    public static async retryAsync<T>(requestFn: () => Promise<T>, retries: number, delayInMs: number): Promise<T> {
        try {
            return await requestFn();
        }
        catch (error) {
            if (retries === 0) {
                throw error;
            }
            else {
                await new Promise((resolve): void => {
                  // tslint:disable-next-line no-string-based-set-timeout
                  setTimeout(resolve, delayInMs);
                });
                return this.retryAsync(requestFn, retries - 1, delayInMs);
            }
        }
    }
}