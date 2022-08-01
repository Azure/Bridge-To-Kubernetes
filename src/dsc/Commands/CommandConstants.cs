// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Exe.Commands
{
    internal static class CommandConstants
    {
        public const string Connect = "connect";
        public const string PrepConnect = "prep-connect";
        public const string CleanConnectCommand = "clean-local-connect";
        public const string RoutingHeaderCommand = "get-routing-header";
        public const string RoutingSupportedCommand = "is-routing-supported";
        public const string CheckCredentialsCommand = "check-credentials";
        public const string ListIngressCommand = "list-ingress";
        public const string ListNamespaceCommand = "list-namespace";
        public const string ListServiceCommand = "list-service";
        public const string ListContextCommand = "list-context";
        public const string RefreshCredentialsCommand = "refresh-credentials";

        // NOTE: Strings in this class are not localized because users won't be calling the CLI directly
        public static class Options
        {
            public const string Version = "--version";

            public static class Help
            {
                public const string Long = "--help";
                public const string Short = "-h";
                public const string Description = "Print this help information and exit";
            }

            public static class ParentProcessId
            {
                public const string Option = "--ppid";
                public const string Description = "The effective parent process ID that dictates lifetime of the CLI process";
            }

            public static class ConnectTargetNamespace
            {
                public const string Option = "--namespace";
                public const string Description = "The namespace containing the resource to redirect locally.";
            }

            public static class ConnectTargetContainer
            {
                public const string Option = "--container";
                public const string Description = "The name of the container to redirect locally.";
            }

            public static class ConnectTargetPod
            {
                public const string Option = "--pod";
                public const string Description = "The name of the pod to redirect locally.";
            }

            public static class ConnectTargetService
            {
                public const string Option = "--service";
                public const string Description = "The name of the service to redirect locally.";
            }

            public static class ConnectWithDeployment
            {
                public const string Option = "--deployment";
                public const string Description = "The name of the deployment to redirect locally.";
            }

            public static class ConnectUpdateScript
            {
                public const string Option = "--script";
                public const string Description = "Generate a script file to update environment variables.";
            }

            public static class ConnectLocalPort
            {
                public const string Option = "--local-port";
                public const string Description = "Application's port number when running locally.";
            }

            public static class ConnectEnv
            {
                public const string Option = "--env";
                public const string Description = "Output a json file with the environment variables.";
            }

            public static class ControlPort
            {
                public const string Option = "--control-port";
                public const string Description = "A port used to communicate internal status.";
            }

            public static class ElevationRequests
            {
                public const string Option = "--elevation-requests";
                public const string Description = "JSON encoded output from prep-connect command.";
            }

            public static class Routing
            {
                public const string Option = "--routing";
                public const string Description = "The header value to route on.";
            }

            public static class TargetKubeConfigContext
            {
                public const string Option = "--context";
                public const string Description = "The kubeconfig context to use. To use this flag, --namespace must also be passed.";
            }

            public static class UseKubernetesServiceEnvironmentVariables
            {
                public const string Option = "--use-kubernetes-service-environment-variables";
                public const string Description = "An option to run the connect command when cluster service environment variables are used. To learn more, visit https://aka.ms/use-k8s-svc-env-vars";
            }

            public static class Yes
            {
                public const string Long = "--yes";
                public const string Short = "-y";
                public static readonly string Description = "Overrides prompt for confirmation.";
            }

            public static class RoutingManagerFeatureFlag
            {
                public const string Long = "--routing-manager-feature-flag";
                public const string Short = "-rmff";
                public static readonly string Description = "Feature flag to enable for Routing Manager";
            }

            public static class RunContainerized
            {
                public const string Option = "--containerized";
                public const string Description = "An option to run the workload containerized";
            }
        }
    }
}