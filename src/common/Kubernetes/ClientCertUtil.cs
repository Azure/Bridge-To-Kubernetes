// --------------------------------------------------------------------------------------------
//Copyright 2017 the Kubernetes Project

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//   [http://www.apache.org/licenses/LICENSE-2.0](http://www.apache.org/licenses/LICENSE-2.0)

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

//This function is reusing the code from [https://github.com/kubernetes-client/csharp/blob/master/src/KubernetesClient/CertUtils.cs](https://github.com/kubernetes-client/csharp/blob/master/src/KubernetesClient/CertUtils.cs), and was updated to remove code not required in our case.
// --------------------------------------------------------------------------------------------

using k8s;
using k8s.Exceptions;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Microsoft.BridgeToKubernetes.Common.Kubernetes
{
    class ClientCertUtil
    {
        /// <summary>
        /// Retrieves Client Certificate PFX from configuration
        /// </summary>
        /// <param name="config">Kubernetes Client Configuration</param>
        /// <returns>Client certificate PFX</returns>
        public static X509Certificate2 GetClientCert(KubernetesClientConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if ((!string.IsNullOrWhiteSpace(config.ClientCertificateData) ||
                 !string.IsNullOrWhiteSpace(config.ClientCertificateFilePath)) &&
                (!string.IsNullOrWhiteSpace(config.ClientCertificateKeyData) ||
                 !string.IsNullOrWhiteSpace(config.ClientKeyFilePath)))
            {
                return GeneratePfx(config);
            }

            return null;
        }

        /// <summary>
        /// Generates pfx from client configuration
        /// </summary>
        /// <param name="config">Kubernetes Client Configuration</param>
        /// <returns>Generated Pfx Path</returns>
        public static X509Certificate2 GeneratePfx(KubernetesClientConfiguration config)
        {
            string keyData = null;
            string certData = null;

            if (!string.IsNullOrWhiteSpace(config.ClientCertificateKeyData))
            {
                keyData = Encoding.UTF8.GetString(Convert.FromBase64String(config.ClientCertificateKeyData));
            }

            if (!string.IsNullOrWhiteSpace(config.ClientKeyFilePath))
            {
                keyData = File.ReadAllText(config.ClientKeyFilePath);
            }

            if (keyData == null)
            {
                throw new KubeConfigException("keyData is empty");
            }

            if (!string.IsNullOrWhiteSpace(config.ClientCertificateData))
            {
                certData = Encoding.UTF8.GetString(Convert.FromBase64String(config.ClientCertificateData));
            }

            if (!string.IsNullOrWhiteSpace(config.ClientCertificateFilePath))
            {
                certData = File.ReadAllText(config.ClientCertificateFilePath);
            }

            if (certData == null)
            {
                throw new KubeConfigException("certData is empty");
            }

            var cert = X509Certificate2.CreateFromPem(certData, keyData);

            // see https://github.com/kubernetes-client/csharp/issues/737
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (config.ClientCertificateKeyStoreFlags.HasValue)
                {
                    cert = new X509Certificate2(cert.Export(X509ContentType.Pkcs12), "", config.ClientCertificateKeyStoreFlags.Value);
                }
                else
                {
                    cert = new X509Certificate2(cert.Export(X509ContentType.Pkcs12));
                }
            }

            return cert;
        }
    }
}
