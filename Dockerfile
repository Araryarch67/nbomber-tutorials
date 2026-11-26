FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DemoApi/DemoApi.csproj DemoApi/
COPY PerformanceTests/PerformanceTests.csproj PerformanceTests/
RUN dotnet restore DemoApi/DemoApi.csproj && dotnet restore PerformanceTests/PerformanceTests.csproj
COPY DemoApi/ DemoApi/
COPY PerformanceTests/ PerformanceTests/
RUN dotnet publish DemoApi -c Release --no-restore -o /out/api \
 && dotnet publish PerformanceTests -c Release --no-restore -o /out/tests

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /out/api .
# 0.0.0.0 agar bisa dibuka dari host; container tests tetap memakai 127.0.0.1 (network sama).
ENTRYPOINT ["dotnet", "DemoApi.dll", "--urls", "http://0.0.0.0:5080"]

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS tests
WORKDIR /app
COPY --from=build /out/tests .
ENTRYPOINT ["dotnet", "PerformanceTests.dll"]
