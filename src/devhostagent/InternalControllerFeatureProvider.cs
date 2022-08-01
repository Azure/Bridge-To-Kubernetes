// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.BridgeToKubernetes.DevHostAgent
{
    /// <summary>
    /// Extension methods for setting up MVC
    /// </summary>
    public static class MvcExtensions
    {
        /// <summary>
        /// Allows controllers marked as internal
        /// </summary>
        public static IMvcBuilder AllowNonPublicControllers(this IMvcBuilder builder)
        {
            return builder.ConfigureApplicationPartManager(x =>
            {
                x.FeatureProviders.Add(new InternalControllerFeatureProvider());
            });
        }
    }

    /// <summary>
    /// Controller finder that allows Controllers marked as internal
    /// </summary>
    public class InternalControllerFeatureProvider : ControllerFeatureProvider
    {
        /// <summary>
        /// Determines whether a type is a Controller
        /// </summary>
        protected override bool IsController(TypeInfo typeInfo)
        {
            var isInternalController = typeInfo.Assembly == Assembly.GetExecutingAssembly() &&
                                       !typeInfo.IsAbstract &&
                                       typeof(ControllerBase).IsAssignableFrom(typeInfo);
            return isInternalController || base.IsController(typeInfo);
        }
    }
}