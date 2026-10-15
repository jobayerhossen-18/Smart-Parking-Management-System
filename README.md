# Smart Parking Management System

An ASP.NET Core MVC application for managing parking slots, customer vehicles, reservations, parking sessions, fees, and payment records. This repository is a development/portfolio project; it has not been independently build-verified in this packaging environment.

## Technology stack

- C# and ASP.NET Core MVC (.NET 8)
- Entity Framework Core 8
- SQL Server / SQL Server LocalDB
- ASP.NET Core Identity with Admin and Customer roles
- Razor Views, Bootstrap, and Bootstrap Icons
- Optional SSLCOMMERZ hosted-checkout integration (requires your own sandbox credentials and a publicly reachable callback URL)

## Features

- Customer registration, login, profile, and password change
- Vehicle registration and management
- Parking-slot browsing and administration
- Booking and cancellation workflows
- Parking check-in/check-out and fee calculation
- Payment history and receipts
- Admin dashboard, user/rate management, and reports
- Seeded starter slots and parking rates

> Feature presence in source code is not a substitute for testing. Treat payment gateway integration as unverified until tested with SSLCOMMERZ sandbox credentials and callbacks.

## Project structure

```text
SmartParkingMS/
├── Controllers/       MVC controllers
├── Data/              EF Core DbContext and database seeding
├── Models/            Entities, enums, and view models
├── Services/          Payment gateway service
├── Views/             Razor views
├── wwwroot/           CSS and JavaScript
├── Properties/        Local launch profile
├── Program.cs         Dependency injection and request pipeline
├── appsettings.json   Non-secret defaults
└── SmartParkingMS.csproj
```

## Prerequisites

- Visual Studio 2022 with the ASP.NET and web development workload, or the .NET 8 SDK
- SQL Server LocalDB (installed with common Visual Studio workloads) or a SQL Server instance
- Internet access for restoring NuGet packages

## Run locally

1. Clone/download the repository and open `SmartParkingMS.csproj` in Visual Studio, or open a terminal in the project folder.
2. Restore packages:

   ```bash
   dotnet restore
   ```

3. Confirm the `ConnectionStrings:DefaultConnection` value in `appsettings.json` matches your local SQL Server setup. The default is SQL Server LocalDB.
4. Install the EF Core CLI tool if needed:

   ```bash
   dotnet tool install --global dotnet-ef
   ```

5. Apply the migration included in this repository to create/update the local database:

   ```bash
   dotnet ef database update
   ```

6. Configure a local Admin account using User Secrets. Do not put a real password in `appsettings.json` or commit it to Git:

   ```bash
   dotnet user-secrets set "Admin:Email" "your-admin-email@example.com"
   dotnet user-secrets set "Admin:Password" "Use-A-Strong-Unique-Password-Here"
   ```

   Use a password that meets the Identity password policy. The database seeder creates an Admin account only when both settings are present. If they are missing, the app still seeds roles, starter parking slots, and parking rates, but does not create a default Admin account.

7. Run the application:

   ```bash
   dotnet run
   ```

   Alternatively, select the project profile in Visual Studio and press **F5**. On startup, EF Core applies existing migrations and the app seeds roles and starter parking data.

## Local demo payment (optional)

In the `Development` environment, the bKash, Nagad, and Bank Card choices provide a simulated successful payment so the receipt workflow can be demonstrated. This demo does not contact a payment provider and does not charge real money. The demo endpoint is blocked outside `Development`. Never present demo receipts as real financial transactions. For production, configure and validate a real payment gateway such as SSLCOMMERZ.

## SSLCOMMERZ sandbox (optional)

For a real sandbox integration, configure these keys with User Secrets or environment variables; do not commit actual credentials:

```bash
dotnet user-secrets set "SSLCOMMERZ:StoreId" "YOUR_SANDBOX_STORE_ID"
dotnet user-secrets set "SSLCOMMERZ:StorePassword" "YOUR_SANDBOX_STORE_PASSWORD"
dotnet user-secrets set "SSLCOMMERZ:Sandbox" "true"
dotnet user-secrets set "SSLCOMMERZ:BaseUrl" "https://YOUR_PUBLIC_HTTPS_CALLBACK_BASE_URL"
```

The callback base URL must be reachable by the gateway. A localhost URL is not publicly reachable. Test only with the provider's sandbox account until callback validation and payment reconciliation have been verified end-to-end.

## Security notes

- No default Admin password is included in source code. Configure `Admin:Email` and `Admin:Password` using User Secrets or environment variables.
- Do not commit credentials, connection strings containing passwords, private keys, database backups, or production customer data.
- The simulated payment endpoint is available only in Development when explicitly enabled.
- Gateway success is accepted only after server-side validation of the transaction status, transaction ID, amount, and BDT currency. Test these checks with the provider sandbox before relying on them.
- Use HTTPS and appropriate production secrets management for any deployment.

## Known limitations

- The default database connection uses LocalDB and may need to be changed on non-Windows systems or when using a different SQL Server instance.
- SSLCOMMERZ callbacks require valid credentials and a public HTTPS callback URL.
- Before publishing, run `dotnet build` and test the core flows locally. This cleaned package excludes IDE and generated build output; it does not claim that all runtime features have been independently tested.

## Suggested manual test checklist

- Register a Customer and sign in/out.
- Configure an Admin via User Secrets and verify Admin-only pages are protected.
- Add a vehicle, browse available slots, create/cancel a booking.
- Test check-in, check-out, minimum one-hour fee, and payment state transitions.
- Verify a Customer cannot view another Customer's booking/payment.
- Test demo payment only in Development with the explicit setting enabled.
- If configured, test SSLCOMMERZ success/fail/cancel callbacks using sandbox transactions.


## Screenshots
Given in screenshots folder
