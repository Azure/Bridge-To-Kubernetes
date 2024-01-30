FROM mcr.microsoft.com/dotnet/aspnet:7.0@sha256:9658a001bc235f15d2d7f3a68f90cb1897f2fdc3e2e74161605c076d3472e39c

COPY bin/Release/net7.0/publish/ App/
WORKDIR /App
ENTRYPOINT ["dotnet", "ConnectSQLDatabase.dll"]