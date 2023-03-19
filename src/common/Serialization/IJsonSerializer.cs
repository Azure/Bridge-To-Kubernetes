// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Serialization
{
  internal interface IJsonSerializer
  {
    T DeserializeObject<T>(string v);
    T DeserializeObjectCaseInsensitive<T>(string v);

    string SerializeObject(object obj);
    string SerializeObjectIndented(object obj);
  }
}
