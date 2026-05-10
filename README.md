# 🛒 EcomPlatform API

Multi-tenant E-commerce Platform built with ASP.NET Core 10, Clean Architecture, Entity Framework Core, and SQL Server.

---

## ⚙️ Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (Express or full)
- [Postman](https://www.postman.com/downloads/)

---

## 🚀 Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/SeifDesouky/Ecom-Platform.git
cd Ecom-Platform/EcomPlatform
```

### 2. Configure `appsettings.json`

Open `EcomPlatform.API/appsettings.json` and update the following:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER\\SQLEXPRESS;Database=EcomPlatformDB;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "JwtSettings": {
    "SecretKey": "any-long-secret-key-min-32-chars-here"
  },
  "EmailSettings": {
    "Username": "your-email@gmail.com",
    "Password": "your-app-password"
  },
  "CloudinarySettings": {
    "CloudName": "your-cloud-name",
    "ApiKey": "your-api-key",
    "ApiSecret": "your-api-secret"
  }
}
```

> 💡 **Cloudinary & Email** are optional for basic testing — the API will still run without them.

### 3. Run the API

```bash
cd EcomPlatform.API
dotnet run
```

The API will automatically:
- ✅ Create the database
- ✅ Run all migrations
- ✅ Seed initial data (SuperAdmin, Plans, Settings)

API will be available at: `http://localhost:5000`  
Swagger UI: `http://localhost:5000/swagger`

---

## 🧪 Testing with Postman

### Import the Collection

1. Open Postman
2. Click **Import**
3. Select the file: `EcomPlatform_API_v1_Complete.postman_collection.json`

### First Request — Login as SuperAdmin

```
POST /api/auth/login
```

```json
{
  "email": "admin@ecomplatform.com",
  "password": "Admin@123456"
}
```

Copy the `token` from the response and use it as:
```
Authorization: Bearer <token>
```

---

## 🏗️ Project Structure

```
EcomPlatform/
├── EcomPlatform.API/           # Controllers, Middlewares, Program.cs
├── EcomPlatform.Application/   # DTOs, Services Interfaces, Validators
├── EcomPlatform.Core/          # Entities, Enums, Repository Interfaces
├── EcomPlatform.Infrastructure/# EF Core, Migrations, Services Implementations
└── EcomPlatform.Shared/        # Settings, Shared Models
```

---

## 🔑 Key Features

- ✅ Multi-Tenant Architecture with Tenant Enforcement
- ✅ JWT Authentication + Refresh Token Rotation
- ✅ Role-Based Authorization (SuperAdmin / Admin / User)
- ✅ Products, Categories, Orders, Customers
- ✅ Coupons & Discounts
- ✅ Plans & Subscriptions
- ✅ Invoices & Payments
- ✅ Shipping Methods & Zones
- ✅ Notifications System
- ✅ CMS (Articles & Pages)
- ✅ Audit Logs
- ✅ Dashboard & Analytics
- ✅ File Uploads (Cloudinary)
- ✅ Email Service
- ✅ ZATCA Integration
- ✅ Forgot/Reset Password
- ✅ Email Verification
