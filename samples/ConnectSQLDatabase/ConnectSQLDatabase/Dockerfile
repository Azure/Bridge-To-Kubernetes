FROM mcr.microsoft.com/dotnet/aspnet:8.0@sha256:3ff67792728179308c4bf06799d8b45d155c60fddc347acf69465a496d9a20b8

COPY bin/Release/net7.0/publish/ App/
WORKDIR /App
ENTRYPOINT ["dotnet", "ConnectSQLDatabase.dll"]