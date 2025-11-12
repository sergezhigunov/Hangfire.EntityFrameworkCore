# Hangfire.EntityFrameworkCore

[![License MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Latest version](https://img.shields.io/nuget/v/Hangfire.EntityFrameworkCore.svg)](https://www.nuget.org/packages/Hangfire.EntityFrameworkCore)

## Overview

An [Entity Framework Core](https://github.com/aspnet/EntityFrameworkCore) provider-neutral job storage implementation for [Hangfire](https://www.hangfire.io) developed by Sergey Odinokov.

## Installation

To install Hangfire Entity Framework Core Storage, run the following command in the Terminal:

```dotnetcli
dotnet add package Hangfire.EntityFrameworkCore
```

Alternatively run the following command in the Nuget Package Manager Console:

```powershell
PM> Install-Package Hangfire.EntityFrameworkCore
```

### ASP.NET

After installation, update your existing [OWIN Startup](https://docs.microsoft.com/en-us/aspnet/aspnet/overview/owin-and-katana/owin-startup-class-detection) with the following lines of code:

```csharp
public void Configuration(IAppBuilder app)
{
    // Register Entity Framework Core Storage
    GlobalConfiguration.Configuration.UseEFCoreStorage(
        // Configure Entity Framework Core to connect database, e.g. SQL Server
        builder => builder.UseSqlServer("Data Source=(LocalDB)\\MSSQLLocalDB;Database=Hangfire"),
        // Optionally configure Entity Framework Core Storage
        new EFCoreStorageOptions()).
        // Optionally register database creator
        UseDatabaseCreator();

    // Configure Hangfire Server and/or Hangfire Dashboard
    app.UseHangfireServer();
    app.UseHangfireDashboard();
}
```

### ASP.NET Core

There is an [example](samples/Hangfire.EntityFrameworkCore.AspNetCore/Program.cs) to use Hangfire.EntityFrameworkCore with ASP.NET Core.

### Migrations

Currently, automatic migrations are not implemented. The migrations support [planned](https://github.com/sergezhigunov/Hangfire.EntityFrameworkCore/issues/1) and will be implemented on future releases.

### Using your own DbContext

As of the `0.3.0` version you have the ability to attach the tables required for this library to your own DbContext. Since the tables are attached to your own DbContext this means that the migrations are also attached to this DbContext and managed by the regular `dotnet ef` migration flow.

There is an example of this configuration found [here](samples/Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext/Program.cs), however the important sections are listed below.

In `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("HangfireConnection")
    ?? throw new InvalidOperationException("Connection string 'HangfireConnection' not found.");

builder.Services.AddDbContextFactory<SampleDbContext>(builder => builder.UseSqlite(connectionString));
builder.Services.AddHangfire((serviceProvider, configuration) =>
    configuration.UseEFCoreStorage(
        () => serviceProvider.GetRequiredService<IDbContextFactory<SampleDbContext>>().CreateDbContext(),
        new EFCoreStorageOptions
        {
            CountersAggregationInterval = new TimeSpan(0, 5, 0),
            DistributedLockTimeout = new TimeSpan(0, 10, 0),
            JobExpirationCheckInterval = new TimeSpan(0, 30, 0),
            QueuePollInterval = new TimeSpan(0, 0, 15),
            Schema = string.Empty,
            SlidingInvisibilityTimeout = new TimeSpan(0, 5, 0),
        }));
```

And then in the `OnModelCreating` method of the DbContext class:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.OnHangfireModelCreating();
}
```

### Queue providers

There is only [built-in SQL-based internal queue provider](src/Hangfire.EntityFrameworkCore/EFCoreJobQueueProvider.cs) supported. [Additional providers support](https://github.com/sergezhigunov/Hangfire.EntityFrameworkCore/issues/2) will be implemented in future.

## License

Hangfire.EntityFrameworkCore licensed under the [MIT License](https://raw.githubusercontent.com/sergezhigunov/Hangfire.EntityFrameworkCore/master/LICENSE).
