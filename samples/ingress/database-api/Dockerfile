FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim@sha256:c30ae929a3cfef278f140c6e2ec050ea2bbab17cfc6b3426c8fe806d9fb3b6dd AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster@sha256:ed737e6920105e2a5cdcd13b9697e7973011e5619339ea64e30871ba0b53fec8 AS build
WORKDIR /src
COPY ["databaseApi.csproj", ""]
RUN dotnet restore "./databaseApi.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "databaseApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "databaseApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "databaseApi.dll"]