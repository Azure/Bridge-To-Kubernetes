FROM mcr.microsoft.com/dotnet/aspnet:8.0@sha256:789045ecae51d62d07877994d567eff4442b7bbd4121867898ee7bf00b7241ea

COPY bin/Release/net7.0/publish/ App/
WORKDIR /App
ENTRYPOINT ["dotnet", "ConnectSQLDatabase.dll"]