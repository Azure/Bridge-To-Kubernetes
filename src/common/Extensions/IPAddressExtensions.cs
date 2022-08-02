// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Linq;

namespace System.Net
{
    internal static class IPAddressExtensions
    {
        public static IPAddress Next(this IPAddress address)
        {
            var bytes = address.GetAddressBytes();
            for (int i = bytes.Length - 1; i >= 1; i--)
            {
                if (bytes[i] != 255)
                {
                    bytes[i]++;
                    break;
                }
                else
                {
                    bytes[i] = 0;
                }
            }
            if (bytes.Last() == 0)
            {
                bytes[bytes.Length - 1] = 1;
            }
            return new IPAddress(bytes);
        }
    }
}