# Build container
FROM mcr.microsoft.com/dotnet/core/sdk:3.1-alpine3.12 AS build-alpine

ARG Configuration=Release
ARG TelemetryType=TELEMETRY_DEVELOPMENT
ARG MindaroBuildNumber=0.0

WORKDIR /src/routingmanager
COPY /src/routingmanager/routingmanager.csproj .
COPY /src/common.json/common.json.csproj /src/common.json/
COPY /src/common/common.csproj /src/common/
RUN dotnet restore
COPY /src/routingmanager/ /src/routingmanager/
COPY /src/common.json/ /src/common.json/
COPY /src/common/ /src/common/
COPY /build/ /build/
ENV TelemetryType=${TelemetryType}
ENV MINDARO_BUILD_NUMBER=${MindaroBuildNumber}

RUN dotnet publish -c ${Configuration} -o /src/publish

# Final container
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-alpine3.12 as final

# Setup common tools
COPY /build/setup-alpine-tools.sh .
RUN chmod +x ./setup-alpine-tools.sh \
    && ./setup-alpine-tools.sh \
    && rm -f ./setup-alpine-tools.sh

# Setup kubectl
COPY /build/setup-kubectl.sh .
RUN chmod +x ./setup-kubectl.sh \
    && ./setup-kubectl.sh \
    && rm -f ./setup-kubectl.sh

WORKDIR /src/routingmanager
COPY --from=build-alpine /src/publish /src/routingmanager
ENTRYPOINT ["dotnet", "/src/routingmanager/Microsoft.BridgeToKubernetes.RoutingManager.dll"]
