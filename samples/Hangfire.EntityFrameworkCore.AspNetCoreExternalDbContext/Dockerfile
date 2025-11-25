FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080


FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["samples/Directory.Build.props", "samples/"]
COPY ["Directory.Build.props", "."]
COPY ["src/Directory.Build.props", "src/"]
COPY ["samples/Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext/Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext.csproj", "samples/Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext/"]
COPY ["src/Hangfire.EntityFrameworkCore/Hangfire.EntityFrameworkCore.csproj", "src/Hangfire.EntityFrameworkCore/"]
COPY ["src/Hangfire.EntityFrameworkCore/ReleaseNotes.txt", "src/Hangfire.EntityFrameworkCore/"]
RUN dotnet restore "./samples/Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext/Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext.csproj"
COPY . .
WORKDIR "/src/samples/Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext"
RUN dotnet build "./Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext.dll"]
