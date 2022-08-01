// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.SignalR.Client
{
    /// <summary>
    /// Extensions for SignalR types
    /// </summary>
    internal static class SignalRExtensions
    {
        /// <summary>
        /// Registers all reflected async methods as handlers to the connection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <param name="impl"></param>
        public static void RegisterAllHandlersFromType<T>(this HubConnection connection, T impl) where T : class
        {
            var handlers = impl.GetType().GetRuntimeMethods().Where(m => typeof(Task).IsAssignableFrom(m.ReturnType));
            foreach (var handler in handlers)
            {
                connection.On(handler.Name, handler.GetParameters().Select(p => p.ParameterType).ToArray(), (parameters) =>
                {
                    return (Task)handler.Invoke(impl, parameters);
                });
            }
        }
    }
}