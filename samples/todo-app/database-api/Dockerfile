FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:7.0-cbl-mariner2.0 AS base
ARG TARGETARCH
WORKDIR /app
EXPOSE 80

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:7.0-cbl-mariner2.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY ["databaseApi.csproj", ""]
RUN dotnet restore "./databaseApi.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "databaseApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "databaseApi.csproj" -c Release -a ${TARGETARCH} -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "databaseApi.dll"]