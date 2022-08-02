// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

export class PromiseUtility {
    // Run asynchronous calls concurrently, respecting the max number of concurrent calls.
    public static async runConcurrentCallsAsync(maxConcurrentCalls: number, promiseLoader: () => Promise<boolean /*shouldContinue*/>): Promise<void> {
        let promisesToCall: Promise<boolean /*shouldContinue*/>[] = [];
        while (true) {
            if (promisesToCall.length >= maxConcurrentCalls) {
                // We have reached maxConcurrentCalls. Let's wait for one of the promises to complete before continuing.
                const shouldContinue: boolean = await Promise.race(promisesToCall);
                if (!shouldContinue) {
                    // We don't have any reasons to keep running promises. Stopping execution.
                    return;
                }
            }

            const promise: Promise<boolean> = promiseLoader();
            if (promise == null) {
                // No more promises to load.
                break;
            }

            const promiseWithCleanup: Promise<boolean> = promise.then((shouldContinue: boolean): boolean => {
                // Remove from promisesToCall the promise that just completed.
                promisesToCall = promisesToCall.filter(p => p !== promiseWithCleanup);
                return shouldContinue;
            });
            promisesToCall.push(promiseWithCleanup);
        }
        // Wait for any promises to complete execution, if any.
        await Promise.all(promisesToCall);
    }
}