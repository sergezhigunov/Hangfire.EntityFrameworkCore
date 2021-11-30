FROM mcr.microsoft.com/dotnet/aspnet:6.0.0-alpine3.14 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0.100-alpine3.14 AS build
WORKDIR /src
COPY ["samples/Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext/Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext.csproj", "samples/Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext/"]
COPY ["src/Hangfire.EntityFrameworkCore/Hangfire.EntityFrameworkCore.csproj", "src/Hangfire.EntityFrameworkCore/"]
RUN dotnet restore "samples/Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext/Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext.csproj"
COPY . .
WORKDIR "/src/samples/Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext"
RUN dotnet build "Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext.dll"]
