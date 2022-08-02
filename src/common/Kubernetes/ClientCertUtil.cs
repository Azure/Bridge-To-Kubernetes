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
using System.Security.Cryptography.X509Certificates;

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
            byte[] keyData = null;
            byte[] certData = null;
            if (!string.IsNullOrWhiteSpace(config.ClientCertificateKeyData))
            {
                keyData = Convert.FromBase64String(config.ClientCertificateKeyData);
            }
            if (!string.IsNullOrWhiteSpace(config.ClientKeyFilePath))
            {
                keyData = File.ReadAllBytes(config.ClientKeyFilePath);
            }
            if (keyData == null)
            {
                throw new KubeConfigException("keyData is empty");
            }
            if (!string.IsNullOrWhiteSpace(config.ClientCertificateData))
            {
                certData = Convert.FromBase64String(config.ClientCertificateData);
            }
            if (!string.IsNullOrWhiteSpace(config.ClientCertificateFilePath))
            {
                certData = File.ReadAllBytes(config.ClientCertificateFilePath);
            }
            if (certData == null)
            {
                throw new KubeConfigException("certData is empty");
            }
            var cert = new Org.BouncyCastle.X509.X509CertificateParser().ReadCertificate(new MemoryStream(certData));
            // key usage is a bit string, zero-th bit is 'digitalSignature'
            // See https://www.alvestrand.no/objectid/2.5.29.15.html for more details.
            if (cert != null && cert.GetKeyUsage() != null && !cert.GetKeyUsage()[0])
            {
                throw new Exception(
                    "Client certificates must be marked for digital signing. " +
                    "See https://github.com/kubernetes-client/csharp/issues/319");
            }
            object obj;
            using (var reader = new StreamReader(new MemoryStream(keyData)))
            {
                obj = new Org.BouncyCastle.OpenSsl.PemReader(reader).ReadObject();
                var key = obj as Org.BouncyCastle.Crypto.AsymmetricCipherKeyPair;
                if (key != null)
                {
                    var cipherKey = key;
                    obj = cipherKey.Private;
                }
            }
            var keyParams = (Org.BouncyCastle.Crypto.AsymmetricKeyParameter)obj;
            var store = new Org.BouncyCastle.Pkcs.Pkcs12StoreBuilder().Build();
            store.SetKeyEntry("K8SKEY", new Org.BouncyCastle.Pkcs.AsymmetricKeyEntry(keyParams), new[] { new Org.BouncyCastle.Pkcs.X509CertificateEntry(cert) });
            using (var pkcs = new MemoryStream())
            {
                store.Save(pkcs, new char[0], new Org.BouncyCastle.Security.SecureRandom());
                if (config.ClientCertificateKeyStoreFlags.HasValue)
                {
                    return new X509Certificate2(pkcs.ToArray(), "", config.ClientCertificateKeyStoreFlags.Value);
                }
                else
                {
                    return new X509Certificate2(pkcs.ToArray());
                }
            }
        }
    }
}
