# Build container
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:7.0-cbl-mariner2.0@sha256:9e054d0663d07e09127d114f753c1068d0bf681eab188352d06f111ce68f050f AS build
ARG TARGETARCH
ARG Configuration=Release
ARG TelemetryType=TELEMETRY_DEVELOPMENT
ARG MindaroBuildNumber=0.0

WORKDIR /src/devhostagent.restorationjob
COPY /src/devhostagent.restorationjob/devhostAgent.restorationjob.csproj .
COPY /src/common/common.csproj /src/common/
RUN dotnet restore -a ${TARGETARCH}
COPY /src/devhostagent.restorationjob/ .
COPY /src/common/ /src/common/
COPY /build/ /build/
ENV TelemetryType=${TelemetryType}
ENV MINDARO_BUILD_NUMBER=${MindaroBuildNumber}

RUN dotnet publish -c ${Configuration} -a ${TARGETARCH} --self-contained false --no-restore -o /output devhostAgent.restorationjob.csproj

# Final container
FROM mcr.microsoft.com/dotnet/aspnet:7.0-cbl-mariner2.0@sha256:ea1751dfd2defec87508aa979088741935d1fd4c870bfd53e7645642fc9ead13 as final

# Setup common tools
RUN tdnf clean all && \
  tdnf check-update && \
  tdnf upgrade -y

RUN tdnf install -y \
  procps \
  bind-utils

WORKDIR /app
COPY --from=build /output /app
ENTRYPOINT ["/app/restorationjob"]

