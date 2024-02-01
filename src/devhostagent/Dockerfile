# Build container
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:7.0-cbl-mariner2.0@sha256:69e18e87d68a6653550c1eed320cdddc3ff39296bb9d04f3317eca6e500daea9 AS build

ARG TARGETARCH
ARG Configuration=Release
ARG TelemetryType=TELEMETRY_DEVELOPMENT
ARG MindaroBuildNumber=0.0

WORKDIR /src/devhostagent
COPY /src/devhostagent/devhostAgent.csproj .
COPY /src/common/common.csproj /src/common/
RUN dotnet restore -a ${TARGETARCH}
COPY /src/devhostagent/ /src/devhostagent/
COPY /src/common/ /src/common/
COPY /build/ /build/
ENV TelemetryType=${TelemetryType}
ENV MINDARO_BUILD_NUMBER=${MindaroBuildNumber}

RUN dotnet publish -c ${Configuration} -a ${TARGETARCH} --self-contained false --no-restore -o /src/publish devhostAgent.csproj

# Final container
FROM mcr.microsoft.com/dotnet/aspnet:7.0-cbl-mariner2.0@sha256:fccd19c673dff30603e1f2ca3ae05234e0c1a2512a30e9b71debaf34a1215664 as final

RUN tdnf clean all && \
 tdnf check-update && \
 tdnf upgrade -y && \
 tdnf install -y bind-utils && \
 tdnf install -y tar

WORKDIR /src/devhostagent
COPY --from=build /src/publish /src/devhostagent
ENTRYPOINT ["dotnet", "/src/devhostagent/Microsoft.BridgeToKubernetes.DevHostAgent.dll"]
