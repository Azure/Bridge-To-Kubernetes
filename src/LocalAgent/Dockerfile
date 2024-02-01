#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0@sha256:9658a001bc235f15d2d7f3a68f90cb1897f2fdc3e2e74161605c076d3472e39c AS base
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