# Hangfire.EntityFrameworkCore

[![License MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Latest version](https://img.shields.io/nuget/v/Hangfire.EntityFrameworkCore.svg)](https://www.nuget.org/packages/Hangfire.EntityFrameworkCore)

## Overview

An [Entity Framework Core](https://github.com/aspnet/EntityFrameworkCore) provider-neutral job storage implementation for [Hangfire](https://www.hangfire.io) developed by Sergey Odinokov.

## Installation

To install Hangfire Entity Framework Core Storage, run the following command in the Nuget Package Manager Console:

```
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

There is an [example](samples/Hangfire.EntityFrameworkCore.AspNetCore/Startup.cs) to use Hangfire.EntityFrameworkCore with ASP.NET Core.

### Migrations

Currently, automatic migrations are not implemented. The migrations support [planned](https://github.com/sergezhigunov/Hangfire.EntityFrameworkCore/issues/1) and will be implemented on future [releases](https://github.com/sergezhigunov/Hangfire.EntityFrameworkCore/milestone/1).

### Queue providers

There is only [built-in SQL-based internal queue provider](src/Hangfire.EntityFrameworkCore/EFCoreJobQueueProvider.cs) supported. [Additional providers support](https://github.com/sergezhigunov/Hangfire.EntityFrameworkCore/issues/2) will be implemented in [future](https://github.com/sergezhigunov/Hangfire.EntityFrameworkCore/milestone/1).

## License

Hangfire.EntityFrameworkCore licensed under the [MIT License](https://raw.githubusercontent.com/sergezhigunov/Hangfire.EntityFrameworkCore/master/LICENSE).
