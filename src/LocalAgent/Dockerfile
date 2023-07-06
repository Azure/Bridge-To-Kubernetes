#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["nuget.config", "."]
COPY ["LocalAgent/localagent.csproj", "LocalAgent/"]
COPY ["library/library.csproj", "library/"]
COPY ["common/common.csproj", "common/"]
RUN dotnet restore "LocalAgent/localagent.csproj"
COPY . .
WORKDIR "/src/LocalAgent"
RUN dotnet build "localagent.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "localagent.csproj" -c Release -o /app/publish

FROM base AS final
RUN apt-get update
RUN apt-get install iptables
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Microsoft.BridgeToKubernetes.LocalAgent.dll"]