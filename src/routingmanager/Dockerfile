# Build container
FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine3.16 AS build-alpine

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
FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine3.16 as final

RUN apk update
RUN apk upgrade
RUN apk add -u bash
RUN apk add -u procps
RUN apk add -u curl
RUN apk add -u bind-tools
RUN apk add -u kubectl --repository=http://dl-cdn.alpinelinux.org/alpine/edge/testing

WORKDIR /src/routingmanager
COPY --from=build-alpine /src/publish /src/routingmanager
ENTRYPOINT ["dotnet", "/src/routingmanager/Microsoft.BridgeToKubernetes.RoutingManager.dll"]
