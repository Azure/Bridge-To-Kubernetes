FROM mcr.microsoft.com/dotnet/aspnet:7.0

COPY bin/Release/net7.0/publish/ App/
WORKDIR /App
ENTRYPOINT ["dotnet", "ConnectSQLDatabase.dll"]