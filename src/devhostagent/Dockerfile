# Build container
FROM mcr.microsoft.com/dotnet/core/sdk:3.1-alpine3.12 AS build-alpine

ARG Configuration=Release
ARG TelemetryType=TELEMETRY_DEVELOPMENT
ARG MindaroBuildNumber=0.0

WORKDIR /src/devhostagent
COPY /src/devhostagent/devhostAgent.csproj .
COPY /src/common.json/common.json.csproj /src/common.json/
COPY /src/common/common.csproj /src/common/
RUN dotnet restore
COPY /src/devhostagent/ /src/devhostagent/
COPY /src/common.json/ /src/common.json/
COPY /src/common/ /src/common/
COPY /build/ /build/
ENV TelemetryType=${TelemetryType}
ENV MINDARO_BUILD_NUMBER=${MindaroBuildNumber}

RUN dotnet publish -c ${Configuration} -o /src/publish -r linux-musl-x64 devhostAgent.csproj

# Final container
FROM mcr.microsoft.com/dotnet/core/runtime:3.1-alpine3.12 as final
WORKDIR /src/devhostagent
COPY --from=build-alpine /src/publish /src/devhostagent
ENTRYPOINT ["dotnet", "/src/devhostagent/Microsoft.BridgeToKubernetes.DevHostAgent.dll"]