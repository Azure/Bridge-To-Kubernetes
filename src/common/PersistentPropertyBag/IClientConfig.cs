// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.BridgeToKubernetes.Common.PersistentProperyBag
{
    internal interface IClientConfig
    {
        /// <summary>
        ///     Persists any in-memory changes to the store to disk. NOTE: First letter of any key will be converted to lowercase. This can cause problems when retrieving keys.
        /// </summary>
        void Persist();

        /// <summary>
        ///     Removes all properties from the in-memory copy of the PropertyBag.
        /// </summary>
        void Clear();

        /// <summary>
        ///     Gets readonly copy of all properties with undefined order from the in memory copy of the store. Use the property bag methods for making changes to the in memory PropertyBag.
        /// </summary>
        /// <returns>A cloned copy of all properties from the PropertyBag.</returns>
        IEnumerable<KeyValuePair<string, object>> GetAllProperties();

        /// <summary>
        ///     Gets a specific property value from the in-memory copy or the PropertyBag.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        object GetProperty(string propertyName);

        /// <summary>
        ///     Removes a specific property value from the in-memory copy of the PropertyBag.
        /// </summary>
        /// <param name="propertyName"></param>
        void RemoveProperty(string propertyName);

        /// <summary>
        ///     Sets a new integer property value in the in-memory copy of the PropertyBag.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        void SetProperty(string propertyName, int value);

        /// <summary>
        ///     Sets a new string property value in the in-memory copy of the PropertyBag.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        void SetProperty(string propertyName, string value);
    }
}