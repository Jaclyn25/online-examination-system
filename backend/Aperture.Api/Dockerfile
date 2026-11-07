FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY backend/Aperture.Api/Aperture.Api.csproj backend/Aperture.Api/
RUN --mount=type=cache,target=/root/.nuget/packages dotnet restore backend/Aperture.Api
COPY backend/Aperture.Api backend/Aperture.Api
RUN --mount=type=cache,target=/root/.nuget/packages dotnet publish backend/Aperture.Api -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out ./
RUN mkdir /app/keys && chown $APP_UID /app/keys
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Aperture.Api.dll"]
