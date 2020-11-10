FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["samples/Hangfire.EntityFrameworkCore.AspNetCore/Hangfire.EntityFrameworkCore.AspNetCore.csproj", "samples/Hangfire.EntityFrameworkCore.AspNetCore/"]
COPY ["src/Hangfire.EntityFrameworkCore/Hangfire.EntityFrameworkCore.csproj", "src/Hangfire.EntityFrameworkCore/"]
RUN dotnet restore "samples/Hangfire.EntityFrameworkCore.AspNetCore/Hangfire.EntityFrameworkCore.AspNetCore.csproj"
COPY . .
WORKDIR "/src/samples/Hangfire.EntityFrameworkCore.AspNetCore"
RUN dotnet build "Hangfire.EntityFrameworkCore.AspNetCore.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Hangfire.EntityFrameworkCore.AspNetCore.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Hangfire.EntityFrameworkCore.AspNetCore.dll"]