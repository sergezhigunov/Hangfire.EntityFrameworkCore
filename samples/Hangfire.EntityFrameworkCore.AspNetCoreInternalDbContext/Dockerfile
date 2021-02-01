FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["samples/Hangfire.EntityFrameworkCore.AspNetCoreInternalDbContext/Hangfire.EntityFrameworkCore.AspNetCoreInternalDbContext.csproj", "samples/Hangfire.EntityFrameworkCore.AspNetCoreInternalDbContext/"]
COPY ["src/Hangfire.EntityFrameworkCore/Hangfire.EntityFrameworkCore.csproj", "src/Hangfire.EntityFrameworkCore/"]
RUN dotnet restore "samples/Hangfire.EntityFrameworkCore.AspNetCoreInternalDbContext/Hangfire.EntityFrameworkCore.AspNetCoreInternalDbContext.csproj"
COPY . .
WORKDIR "/src/samples/Hangfire.EntityFrameworkCore.AspNetCoreInternalDbContext"
RUN dotnet build "Hangfire.EntityFrameworkCore.AspNetCoreInternalDbContext.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Hangfire.EntityFrameworkCore.AspNetCoreInternalDbContext.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Hangfire.EntityFrameworkCore.AspNetCoreInternalDbContext.dll"]