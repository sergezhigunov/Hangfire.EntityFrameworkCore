FROM mcr.microsoft.com/dotnet/aspnet:6.0.0-alpine3.14 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0.100-alpine3.14 AS build
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
