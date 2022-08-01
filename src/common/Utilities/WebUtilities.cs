// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    internal static class WebUtilities
    {
        /// <summary>
        /// Retries a given action for the default number of times.
        /// </summary>
        /// <param name="action">
        /// The action to repeat. Returns true on success (exit), or false on failure (retry).
        /// Action must implement its own exception handling.
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns>True if the function succeeded, otherwise false</returns>
        public static bool Retry(Func<int, bool> action, CancellationToken cancellationToken)
        {
            return Retry(action, 5, cancellationToken);
        }

        /// <summary>
        /// Retries a given action for the specified number of times.
        /// </summary>
        /// <param name="action">
        /// The action to repeat. Returns true on success (exit), or false on failure (retry).
        /// Action must implement its own exception handling.
        /// </param>
        /// <param name="numberOfAttempts">The maximum number of times to retry the action</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True if the function succeeded, otherwise false</returns>
        public static bool Retry(Func<int, bool> action, uint numberOfAttempts, CancellationToken cancellationToken)
        {
            for (int i = 0; i < numberOfAttempts; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (action(i))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Retries a given action for the default number of times.
        /// </summary>
        /// <param name="action">
        /// The async action to repeat. Returns an awaitable bool. True on success (exit), or false on failure (retry).
        /// Action must implement its own exception handling.
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns>True if the function succeeded, otherwise false</returns>
        public static async Task<bool> RetryAsync(Func<Task<bool>> action, CancellationToken cancellationToken)
        {
            return await RetryAsync((int unused) => action(), 10, cancellationToken);
        }

        public static async Task<bool> RetryAsync(Func<int, Task<bool>> action, CancellationToken cancellationToken)
        {
            return await RetryAsync(action, 10, cancellationToken);
        }

        /// <summary>
        /// Retries a given async action for the specified number of times.
        /// </summary>
        /// <param name="action">
        /// The async action to repeat. Returns an awaitable bool. True on success (exit), or false on failure (retry).
        /// Action must implement its own exception handling.
        /// </param>
        /// <param name="numberOfAttempts">The maximum number of times to retry the action</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True if the function succeeded, otherwise false</returns>
        public static async Task<bool> RetryAsync(Func<int, Task<bool>> action, uint numberOfAttempts, CancellationToken cancellationToken)
        {
            for (int i = 0; i < numberOfAttempts; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await action(i))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Retries a given async action until the action succeeds or the total time elapsed exceeds the maxWaitTime.
        /// </summary>
        /// <param name="action">The async action to repeat. Returns an awaitable bool. True on success (exit), or false on failure (retry).
        /// Action must implement its own exception handling.</param>
        /// <param name="maxWaitTime">The maximum amount of time that the retries will be tried for.</param>
        /// <param name="cancellationToken">Optional cancellationToken to check between loops.</param>
        /// <exception cref="OperationCanceledException"></exception>
        /// <returns>True if the function succeeded, otherwise false</returns>
        public static async Task<bool> RetryUntilTimeAsync(Func<TimeSpan, Task<bool>> action, TimeSpan maxWaitTime, CancellationToken cancellationToken)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            while (stopWatch.Elapsed < maxWaitTime)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await action(stopWatch.Elapsed))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Retries a given async action until the action succeeds or the total time elapsed exceeds the maxWaitTime and waits for waitInterval between each retry.
        /// </summary>
        /// <param name="action">The async action to repeat. Returns an awaitable bool. True on success (exit), or false on failure (retry).
        /// Action must implement its own exception handling.</param>
        /// <param name="maxWaitTime">The maximum amount of time that the retries will be tried for.</param>
        /// <param name="waitInterval">Wait interval between each retry.</param>
        /// <param name="cancellationToken">Optional cancellationToken to check between loops.</param>
        /// <exception cref="OperationCanceledException"></exception>
        /// <returns>True if the function succeeded, otherwise false</returns>
        public static async Task<bool> RetryUntilTimeWithWaitAsync(Func<TimeSpan, Task<bool>> action, TimeSpan maxWaitTime, TimeSpan waitInterval, CancellationToken cancellationToken)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            while (stopWatch.Elapsed < maxWaitTime)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await action(stopWatch.Elapsed))
                {
                    return true;
                }

                await Task.Delay(waitInterval);
            }

            return false;
        }

        /// <summary>
        /// Retries a given async action for the specified number of times or until the max wait time is reached with exponential backoff.
        /// </summary>
        /// <param name="func">
        /// The async action to repeat. Returns an awaitable bool. True on success (exit), or false on failure (retry).
        /// Action must implement its own exception handling. Action isn't required to add a wait before retry.
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <param name="maxWaitTimeInSeconds">The maximum amount of time in seconds that the retries will be tried for. Default is 300 (5min).</param>
        /// <param name="numberOfAttempts">The maximum number of times to retry the action. Default is 10.</param>
        /// <param name="delayIntervalInMilliseconds">
        /// Delay interval between retries. Default is 500ms with exponential backoff.
        /// </param>
        /// <param name="maxDelayIntervalInSeconds">
        /// Every failure doubles the delay time for the next retry until it reaches this value.
        /// Then it will be equal to maxDelayIntervalInSeconds seconds for the rest of the retries.
        /// </param>
        /// <returns>True if the function succeeded, otherwise false</returns>
        public static async Task<bool> RetryWithExponentialBackoffAsync(
            Func<int, Task<bool>> func,
            CancellationToken cancellationToken,
            long maxWaitTimeInSeconds = 300,
            uint numberOfAttempts = 10,
            long delayIntervalInMilliseconds = 500,
            long maxDelayIntervalInSeconds = 30)
        {
            TimeSpan maxWaitTime = TimeSpan.FromSeconds(maxWaitTimeInSeconds);
            TimeSpan maxDelayInterval = TimeSpan.FromSeconds(maxDelayIntervalInSeconds);
            var delay = TimeSpan.FromMilliseconds(delayIntervalInMilliseconds);
            var stopWatch = Stopwatch.StartNew();
            for (int i = 1; i <= numberOfAttempts && stopWatch.Elapsed < maxWaitTime; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await func(i))
                {
                    return true;
                }
                else
                {
                    // Wait and retry
                    await Task.Delay(delay, cancellationToken);

                    // Increase the delay for next retry
                    delay = TimeSpan.FromSeconds(Math.Min(2 * delay.TotalSeconds, maxDelayInterval.TotalSeconds));
                }
            }

            return false;
        }
    }
}