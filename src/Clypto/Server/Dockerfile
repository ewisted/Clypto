#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["Clypto/Server/Clypto.Server.csproj", "Clypto/Server/"]
COPY ["Clypto/Client/Clypto.Client.csproj", "Clypto/Client/"]
COPY ["Clypto/Shared/Clypto.Shared.csproj", "Clypto/Shared/"]
RUN dotnet restore "Clypto/Server/Clypto.Server.csproj"
COPY . .
WORKDIR "/src/Clypto/Server"
RUN dotnet build "Clypto.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Clypto.Server.csproj" -c Release -o /app/publish

WORKDIR /app
# Install required runtime dependencies
RUN apt-get update && apt-get install -y ffmpeg

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir /app/clips
ENTRYPOINT ["dotnet", "Clypto.Server.dll"]