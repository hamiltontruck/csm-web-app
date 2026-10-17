FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Razor files must be present during restore so .NET includes the Blazor framework assets.
COPY CSM.Web/ CSM.Web/
RUN dotnet restore CSM.Web/CSM.Web.csproj
RUN dotnet publish CSM.Web/CSM.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CSM.Web.dll"]
