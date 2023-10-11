using System.Threading;

namespace Microsoft.BridgeToKubernetes.Library.EndpointManagement
{
    public interface IEndpointManagerLauncher
    {
        void LaunchEndpointManager(string currentUserName, string socketFilePath, string logFileDirectory, CancellationToken cancellationToken);
    }
}
