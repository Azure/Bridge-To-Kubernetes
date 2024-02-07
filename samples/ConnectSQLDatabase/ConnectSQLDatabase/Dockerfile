FROM mcr.microsoft.com/dotnet/aspnet:8.0@sha256:1d6ca86c35975c08bd63a066f190fb7bd68fbc2d675d70b2728d458921743a24

COPY bin/Release/net7.0/publish/ App/
WORKDIR /App
ENTRYPOINT ["dotnet", "ConnectSQLDatabase.dll"]