# Build container
FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine3.16 AS build-alpine

ARG Configuration=Release
ARG TelemetryType=TELEMETRY_DEVELOPMENT
ARG MindaroBuildNumber=0.0

WORKDIR /src/devhostagent.restorationjob
COPY /src/devhostagent.restorationjob/devhostAgent.restorationjob.csproj .
COPY /src/common.json/common.json.csproj /src/common.json/
COPY /src/common/common.csproj /src/common/
RUN dotnet restore
COPY /src/devhostagent.restorationjob/ .
COPY /src/common.json/ /src/common.json/
COPY /src/common/ /src/common/
COPY /build/ /build/
ENV TelemetryType=${TelemetryType}
ENV MINDARO_BUILD_NUMBER=${MindaroBuildNumber}

RUN dotnet publish -c ${Configuration} -o /output devhostAgent.restorationjob.csproj

# Final container
FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine3.16 as final

# Setup common tools
COPY /build/setup-alpine-tools.sh .
COPY /build/setup-kubectl.sh .
RUN chmod +x ./setup-alpine-tools.sh ./setup-kubectl.sh \
    && ./setup-alpine-tools.sh \
    && ./setup-kubectl.sh \
    && rm -f ./setup-alpine-tools.sh ./setup-kubectl.sh

WORKDIR /app
ENTRYPOINT ["/app/restorationjob"]

COPY --from=build-alpine /output /app