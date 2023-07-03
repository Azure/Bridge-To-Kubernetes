# Build container
FROM mcr.microsoft.com/dotnet/sdk:7.0-cbl-mariner2.0 AS build

ARG Configuration=Release
ARG TelemetryType=TELEMETRY_DEVELOPMENT
ARG MindaroBuildNumber=0.0

WORKDIR /src/routingmanager
COPY /src/routingmanager/routingmanager.csproj .
COPY /src/common/common.csproj /src/common/
RUN dotnet restore
COPY /src/routingmanager/ /src/routingmanager/
COPY /src/common/ /src/common/
COPY /build/ /build/
ENV TelemetryType=${TelemetryType}
ENV MINDARO_BUILD_NUMBER=${MindaroBuildNumber}

RUN dotnet publish -c ${Configuration} -o /src/publish

# Final container
FROM mcr.microsoft.com/dotnet/aspnet:7.0-cbl-mariner2.0 as final

# Setup common tools
RUN tdnf clean all && \
  tdnf check-update && \
  tdnf upgrade -y

RUN tdnf install -y \
  procps \
  bind-utils

COPY /build/setup-kubectl.sh .
RUN chmod +x ./setup-kubectl.sh \
    && ./setup-kubectl.sh \
    && rm -f ./setup-kubectl.sh

WORKDIR /src/routingmanager
COPY --from=build /src/publish /src/routingmanager
ENTRYPOINT ["dotnet", "/src/routingmanager/Microsoft.BridgeToKubernetes.RoutingManager.dll"]
