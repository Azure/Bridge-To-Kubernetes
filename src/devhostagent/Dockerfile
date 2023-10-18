# Build container
FROM mcr.microsoft.com/dotnet/sdk:7.0-cbl-mariner2.0 AS build

RUN tdnf clean all && \
 tdnf check-update && \
 tdnf upgrade -y  

ARG Configuration=Release
ARG TelemetryType=TELEMETRY_DEVELOPMENT
ARG MindaroBuildNumber=0.0

WORKDIR /src/devhostagent
COPY /src/devhostagent/devhostAgent.csproj .
COPY /src/common/common.csproj /src/common/
RUN dotnet restore
COPY /src/devhostagent/ /src/devhostagent/
COPY /src/common/ /src/common/
COPY /build/ /build/
ENV TelemetryType=${TelemetryType}
ENV MINDARO_BUILD_NUMBER=${MindaroBuildNumber}

RUN dotnet publish -c ${Configuration} -o /src/publish devhostAgent.csproj

# Final container
FROM mcr.microsoft.com/dotnet/aspnet:7.0-cbl-mariner2.0 as final

RUN tdnf clean all && \
 tdnf check-update && \
 tdnf upgrade -y && \
 tdnf install -y bind-utils && \
 tdnf install -y tar

WORKDIR /src/devhostagent
COPY --from=build /src/publish /src/devhostagent
ENTRYPOINT ["dotnet", "/src/devhostagent/Microsoft.BridgeToKubernetes.DevHostAgent.dll"]
