// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Microsoft.BridgeToKubernetes.Common.Exceptions;

namespace System.Threading.Tasks
{
    internal static class TaskExtensions
    {
        public static void Forget(this Task task)
        {
        }

        public static bool CompletedSuccessfully(this Task task)
        {
            return task.Status == TaskStatus.RanToCompletion;
        }

        /// <summary>
        /// Returns true if the Task is running or still waiting to be executed. (Not terminated)
        /// </summary>
        public static bool IsRunning(this Task task)
        {
            return task.Status == TaskStatus.Running || task.Status == TaskStatus.WaitingForActivation;
        }

        /// <summary>
        /// Performs a Task.WhenAny(), but ensures the first completed Task was successful.
        /// </summary>
        public static async Task<Task> WhenAnyWithErrorPropagation(this IEnumerable<Task> tasks)
        {
            var t = await Task.WhenAny(tasks);
            if (t.IsFaulted || t.IsCanceled)
            {
                if (t.Exception != null)
                {
                    ExceptionDispatchInfo.Capture(t.Exception).Throw();
                    throw new UnexpectedStateException($"{nameof(ExceptionDispatchInfo)} didn't throw!", t.Exception, null);
                }
                else
                {
                    throw new TaskCanceledException();
                }
            }

            return t;
        }

        /// <summary>
        /// Performs a Task.WhenAny(), but ensures the first completed Task was successful.
        /// </summary>
        public static async Task<Task<T>> WhenAnyWithErrorPropagation<T>(this IEnumerable<Task<T>> tasks)
        {
            var t = await Task.WhenAny(tasks);
            if (t.IsFaulted || t.IsCanceled)
            {
                if (t.Exception != null)
                {
                    ExceptionDispatchInfo.Capture(t.Exception).Throw();
                    throw new UnexpectedStateException($"{nameof(ExceptionDispatchInfo)} didn't throw!", t.Exception, null);
                }
                else
                {
                    throw new TaskCanceledException();
                }
            }

            return t;
        }

        #region WhenAnyWithErrorPropagation ValueTuple overrides - Task

        /// <summary>
        /// Performs a Task.WhenAny(), but ensures the first completed Task was successful.
        /// </summary>
        public static Task<Task> WhenAnyWithErrorPropagation(this ValueTuple<Task, Task> tasks)
        {
            return WhenAnyWithErrorPropagation(new[] { tasks.Item1, tasks.Item2 });
        }

        /// <summary>
        /// Performs a Task.WhenAny(), but ensures the first completed Task was successful.
        /// </summary>
        public static Task<Task> WhenAnyWithErrorPropagation(this ValueTuple<Task, Task, Task> tasks)
        {
            return WhenAnyWithErrorPropagation(new[] { tasks.Item1, tasks.Item2, tasks.Item3 });
        }

        /// <summary>
        /// Performs a Task.WhenAny(), but ensures the first completed Task was successful.
        /// </summary>
        public static Task<Task> WhenAnyWithErrorPropagation(this ValueTuple<Task, Task, Task, Task> tasks)
        {
            return WhenAnyWithErrorPropagation(new[] { tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4 });
        }

        /// <summary>
        /// Performs a Task.WhenAny(), but ensures the first completed Task was successful.
        /// </summary>
        public static Task<Task> WhenAnyWithErrorPropagation(this ValueTuple<Task, Task, Task, Task, Task> tasks)
        {
            return WhenAnyWithErrorPropagation(new[] { tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5 });
        }

        /// <summary>
        /// Performs a Task.WhenAny(), but ensures the first completed Task was successful.
        /// </summary>
        public static Task<Task> WhenAnyWithErrorPropagation(this ValueTuple<Task, Task, Task, Task, Task, Task> tasks)
        {
            return WhenAnyWithErrorPropagation(new[] { tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6 });
        }

        /// <summary>
        /// Performs a Task.WhenAny(), but ensures the first completed Task was successful.
        /// </summary>
        public static Task<Task> WhenAnyWithErrorPropagation(this ValueTuple<Task, Task, Task, Task, Task, Task, Task> tasks)
        {
            return WhenAnyWithErrorPropagation(new[] { tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7 });
        }

        #endregion WhenAnyWithErrorPropagation ValueTuple overrides - Task

        #region WhenAnyWithErrorPropagation ValueTuple overrides - Task<T>

        /// <summary>
        /// Performs a Task.WhenAny(), but ensures the first completed Task was successful.
        /// </summary>
        public static Task<Task<T>> WhenAnyWithErrorPropagation<T>(this ValueTuple<Task<T>, Task<T>> tasks)
        {
            return WhenAnyWithErrorPropagation(new[] { tasks.Item1, tasks.Item2 });
        }

        /// <summary>
        /// Performs a Task.WhenAny(), but ensures the first completed Task was successful.
        /// </summary>
        public static Task<Task<T>> WhenAnyWithErrorPropagation<T>(this ValueTuple<Task<T>, Task<T>, Task<T>> tasks)
        {
            return WhenAnyWithErrorPropagation(new[] { tasks.Item1, tasks.Item2, tasks.Item3 });
        }

        /// <summary>
        /// Performs a Task.WhenAny(), but ensures the first completed Task was successful.
        /// </summary>
        public static Task<Task<T>> WhenAnyWithErrorPropagation<T>(this ValueTuple<Task<T>, Task<T>, Task<T>, Task<T>> tasks)
        {
            return WhenAnyWithErrorPropagation(new[] { tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4 });
        }

        /// <summary>
        /// Performs a Task.WhenAny(), but ensures the first completed Task was successful.
        /// </summary>
        public static Task<Task<T>> WhenAnyWithErrorPropagation<T>(this ValueTuple<Task<T>, Task<T>, Task<T>, Task<T>, Task<T>> tasks)
        {
            return WhenAnyWithErrorPropagation(new[] { tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5 });
        }

        /// <summary>
        /// Performs a Task.WhenAny(), but ensures the first completed Task was successful.
        /// </summary>
        public static Task<Task<T>> WhenAnyWithErrorPropagation<T>(this ValueTuple<Task<T>, Task<T>, Task<T>, Task<T>, Task<T>, Task<T>> tasks)
        {
            return WhenAnyWithErrorPropagation(new[] { tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6 });
        }

        /// <summary>
        /// Performs a Task.WhenAny(), but ensures the first completed Task was successful.
        /// </summary>
        public static Task<Task<T>> WhenAnyWithErrorPropagation<T>(this ValueTuple<Task<T>, Task<T>, Task<T>, Task<T>, Task<T>, Task<T>, Task<T>> tasks)
        {
            return WhenAnyWithErrorPropagation(new[] { tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4, tasks.Item5, tasks.Item6, tasks.Item7 });
        }

        #endregion WhenAnyWithErrorPropagation ValueTuple overrides - Task<T>
    }
}