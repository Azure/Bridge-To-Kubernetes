#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY "mi-webapp.csproj" .

RUN dotnet restore "mi-webapp.csproj"

COPY . .

WORKDIR "/src/."
RUN dotnet build "mi-webapp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "mi-webapp.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN apt-get update -y
RUN apt-get upgrade -y
RUN apt-get install -y libsecret-1-0
ENTRYPOINT ["dotnet", "mi-webapp.dll"]