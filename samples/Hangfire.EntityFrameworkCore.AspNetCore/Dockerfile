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
COPY ["samples/Hangfire.EntityFrameworkCore.AspNetCore/Hangfire.EntityFrameworkCore.AspNetCore.csproj", "samples/Hangfire.EntityFrameworkCore.AspNetCore/"]
COPY ["src/Hangfire.EntityFrameworkCore/Hangfire.EntityFrameworkCore.csproj", "src/Hangfire.EntityFrameworkCore/"]
COPY ["src/Hangfire.EntityFrameworkCore/ReleaseNotes.txt", "src/Hangfire.EntityFrameworkCore/"]
RUN dotnet restore "./samples/Hangfire.EntityFrameworkCore.AspNetCore/Hangfire.EntityFrameworkCore.AspNetCore.csproj"
COPY . .
WORKDIR "/src/samples/Hangfire.EntityFrameworkCore.AspNetCore"
RUN dotnet build "./Hangfire.EntityFrameworkCore.AspNetCore.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Hangfire.EntityFrameworkCore.AspNetCore.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Hangfire.EntityFrameworkCore.AspNetCore.dll"]
