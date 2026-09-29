# DentalPro

## Build and test

The solution targets .NET 10. Restore dependencies, then build and run tests:

```powershell
dotnet restore DentalPro.sln
dotnet build DentalPro.sln --no-restore
dotnet test DentalPro.sln --no-build
```

The API project is an empty ASP.NET Core host. Domain and feature behavior
will be added in later implementation work.
