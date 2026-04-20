FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src

COPY src/backend/src/Mahjong.Autotable.Api ./src/backend/src/Mahjong.Autotable.Api
RUN dotnet restore ./src/backend/src/Mahjong.Autotable.Api/Mahjong.Autotable.Api.csproj
RUN dotnet publish ./src/backend/src/Mahjong.Autotable.Api/Mahjong.Autotable.Api.csproj -c Release -o /app/publish

FROM node:24-alpine AS modern-build
WORKDIR /src
COPY src/frontend/modern/package*.json ./
RUN npm install
COPY src/frontend/modern ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime-autotable
WORKDIR /app
COPY --from=backend-build /app/publish ./
COPY src/frontend/autotable ./wwwroot/autotable
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Mahjong.Autotable.Api.dll"]

FROM runtime-autotable AS runtime-modern
COPY --from=modern-build /src/dist ./wwwroot
