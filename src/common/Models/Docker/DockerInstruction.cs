// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.Docker
{
    internal enum DockerInstruction
    {
        NONE,
        COMMENT,
        FROM,
        RUN,
        CMD,
        LABEL,
        MAINTAINER,
        EXPOSE,
        ENV,
        ADD,
        COPY,
        ENTRYPOINT,
        VOLUME,
        USER,
        WORKDIR,
        ARG,
        ONBUILD,
        STOPSIGNAL,
        HEALTHCHECK,
        SHELL
    }
}