#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0-cbl-mariner2.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0-cbl-mariner2.0 AS build
WORKDIR /src
COPY ["nuget.config", "."]
COPY ["LocalAgent/LocalAgent.csproj", "LocalAgent/"]
COPY ["library/library.csproj", "library/"]
COPY ["common/common.csproj", "common/"]
RUN dotnet restore "LocalAgent/LocalAgent.csproj"
COPY . .
WORKDIR "/src/LocalAgent"
RUN dotnet build "LocalAgent.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LocalAgent.csproj" -c Release -o /app/publish

FROM base AS final
RUN tdnf clean all & tdnf check-update & tdnf upgrade -y
RUN tdnf install -y iptables
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Microsoft.BridgeToKubernetes.LocalAgent.dll"]