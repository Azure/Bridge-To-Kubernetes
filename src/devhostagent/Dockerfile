# Build container
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0.101-cbl-mariner2.0@sha256:e14034d5ba09b02204d56e809343586475433fd021938488088ccc5cae9d3cd2 AS build

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
FROM mcr.microsoft.com/dotnet/aspnet:7.0-cbl-mariner2.0@sha256:ea1751dfd2defec87508aa979088741935d1fd4c870bfd53e7645642fc9ead13 as final

RUN tdnf clean all && \
 tdnf check-update && \
 tdnf upgrade -y && \
 tdnf install -y bind-utils && \
 tdnf install -y tar

WORKDIR /src/devhostagent
COPY --from=build /src/publish /src/devhostagent
ENTRYPOINT ["dotnet", "/src/devhostagent/Microsoft.BridgeToKubernetes.DevHostAgent.dll"]
