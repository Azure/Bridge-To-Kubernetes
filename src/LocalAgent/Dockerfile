#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0@sha256:1d6ca86c35975c08bd63a066f190fb7bd68fbc2d675d70b2728d458921743a24 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:7ef41132b2ebe6166bde36b7ba2f0d302e10307c3e0523a4539643a77233f56d AS build
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