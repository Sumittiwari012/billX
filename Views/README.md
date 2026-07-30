# HealthSeva Backend API (.NET 8 Web API)

![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)
![PostgreSQL](https://img.shields.io/badge/Database-PostgreSQL-blue.svg)
![EF Core](https://img.shields.io/badge/ORM-Entity%20Framework%20Core%209.0-purple.svg)
![Swagger UI](https://img.shields.io/badge/Documentation-Swagger%20OpenAPI-green.svg)
![xUnit](https://img.shields.io/badge/Testing-xUnit%20Integration-yellow.svg)

---

## 📌 Project Overview

**HealthSeva** is a comprehensive healthcare management backend web API built on **.NET 8** and **PostgreSQL**. It powers home healthcare services, medical staff management, patient booking lifecycles, clinical lab report uploads, and administrative analytics dashboards.

### Core Capabilities:
- **Authentication & Security:** JWT Bearer Token authorization with role-based access control.
- **Customer / Patient Portal:** Complete CRUD operations for customer records, contact associations, and patient histories.
- **Medical Staff Management:** Profiles and dispatch management for Doctors, Nurses, Physiotherapists, and Generic Staff (Lab Technicians, ECG Technicians, Support Staff).
- **Booking & Clinical Workflows:** Booking creation, status tracking, OTP verification, payment tracking, and diagnostic report uploads.
- **Analytics Dashboard:** Real-time metrics for active staff, booking statuses, revenue, and customer metrics.
- **Public & Blog Portal:** Public medical articles, blog posts, category management, and view tracking.
- **Audit System:** Automatic change tracking and backup log recording via EF Core `MBackUp`.

---

## 🛠️ Technology Stack

- **Framework:** .NET 8 Web API (`C# 12`)
- **Database:** PostgreSQL (using `Npgsql.EntityFrameworkCore.PostgreSQL`)
- **ORM:** Entity Framework Core 9.0
- **Authentication:** JWT Bearer Token (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- **Documentation:** Swagger / Swashbuckle OpenAPI 9.0
- **Testing:** `xUnit` with `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`)

---

## 📁 Project Structure

```text
backend/
├── HealthSeva.sln                      # Visual Studio Solution File
├── HealthSeva/                         # Main Web API Project
│   ├── Controllers/                    # API Controllers (Auth, Customer, Booking, Doctor, etc.)
│   ├── DBLayer/                        # DbContext & Database Configurations
│   ├── Models/                         # Data Models & DTOs
│   ├── Services/                       # Business Logic & User Context Services
│   ├── Migrations/                     # EF Core Database Migrations
│   ├── Program.cs                      # Application Entry Point & Middleware Pipeline
│   └── appsettings.json                # Database Connection Strings & App Configs
└── HealthSeva.Tests/                   # Automated xUnit Integration Test Suite
    ├── CustomWebApplicationFactory.cs # Test Server Host Fixture
    ├── AuthApiTests.cs                 # Authentication Integration Tests
    ├── CustomerApiTests.cs             # Customer Portal Tests
    ├── MasterApiTests.cs               # Master Data Endpoint Tests
    ├── DashboardAndBookingApiTests.cs  # Dashboard & Booking Tests
    └── StaffAndRoleApiTests.cs         # Staff Role Endpoint Tests
```

---

## 🚀 Getting Started

### Prerequisites

- **.NET 8 SDK** or higher installed.
- **PostgreSQL 14+** server installed and running.
- **EF Core CLI Tools** (`dotnet-ef`).

### 1. Database Configuration

Update the PostgreSQL connection string in `HealthSeva/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=healthseva;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

### 2. Apply Database Migrations

Apply EF Core migrations to set up the PostgreSQL tables:

```bash
cd HealthSeva
dotnet ef database update
```

### 3. Run the Backend API

Start the .NET Web API application locally:

```bash
dotnet run --urls "http://localhost:5000"
```

The server will start listening at `http://localhost:5000`.

---

## 📖 API Documentation & Swagger UI

Once the application is running, open your web browser and navigate to:

👉 **[http://localhost:5000/swagger](http://localhost:5000/swagger)**

The Swagger UI interactive portal allows you to explore all endpoints, view request/response schemas, and execute live API calls.

---

## 🔑 Authentication Guide

Most protected endpoints require a JWT Bearer Token in the HTTP Request Header:

```text
Authorization: Bearer <your_jwt_token>
```

### Step 1: Login to get token
- **Endpoint:** `POST /api/Auth/authenticate`
- **Request Body:**
  ```json
  {
    "username": "admin",
    "password": "123"
  }
  ```
- **Response:**
  ```json
  {
    "displayName": "admin",
    "role": "Management",
    "token": "eyJhbGciOiJIUzUxMiIsInR5cCI6..."
  }
  ```

### Step 2: Authorize in Swagger or Postman
In **Swagger UI**, click the **Authorize** button at the top right, enter `Bearer <your_token>`, and click **Authorize**.

---

## 📋 API Endpoints Summary

### 🔐 1. Authentication (`/api/Auth`)
| Method | Endpoint | Description | Auth Required |
| :--- | :--- | :--- | :---: |
| `POST` | `/api/Auth/authenticate` | User Login & JWT Token generation | ❌ No |
| `GET` | `/api/Auth/Me` | Get current logged-in user profile | ✅ Yes |

### 📁 2. Master Data (`/api/Master`)
| Method | Endpoint | Description | Auth Required |
| :--- | :--- | :--- | :---: |
| `GET` | `/api/Master/State` | List states | ❌ No |
| `GET` | `/api/Master/District` | List districts | ❌ No |
| `GET` | `/api/Master/ContactType` | List contact types | ❌ No |
| `GET` | `/api/Master/Relation` | List relations | ❌ No |
| `GET` | `/api/Master/ServiceCategory` | List service categories | ❌ No |
| `GET` | `/api/Master/BookingStatus` | List booking status types | ❌ No |
| `GET` | `/api/Master/ServiceProductWithoutId` | List active service products | ❌ No |

### 👤 3. Customer Portal (`/api/Customer`)
| Method | Endpoint | Description | Auth Required |
| :--- | :--- | :--- | :---: |
| `GET` | `/api/Customer/List` | Basic customer list | ✅ Yes |
| `GET` | `/api/Customer/Customer` | Enriched customer list with booking statistics | ✅ Yes |
| `POST` | `/api/Customer/InsertCustomer` | Create new customer & contact record | ✅ Yes |
| `POST` | `/api/Customer/UpdateCustomer` | Update existing customer details | ✅ Yes |
| `GET` | `/api/Customer/Detail/{id}` | Get customer profile details by ID | ✅ Yes |

### 🩺 4. Medical Staff & Dispatches (`/api/Doctor`, `/api/Nursing`, `/api/Physiotherapy`, `/api/Staff`)
| Method | Endpoint | Description | Auth Required |
| :--- | :--- | :--- | :---: |
| `GET` | `/api/Doctor/List` | List active doctors | ✅ Yes |
| `GET` | `/api/Nursing/List` | List active nurses | ✅ Yes |
| `GET` | `/api/Physiotherapy/List` | List active physiotherapists | ✅ Yes |
| `GET` | `/api/Staff/{roleCode}/List` | List generic staff by role (`lab`, `ecg`, `support`) | ✅ Yes |
| `POST` | `/api/Staff` | Register generic staff member | ✅ Yes |
| `POST` | `/api/Staff/Assign` | Assign staff member to a booking | ✅ Yes |

### 📅 5. Bookings & Clinical Reports (`/api/Booking`)
| Method | Endpoint | Description | Auth Required |
| :--- | :--- | :--- | :---: |
| `GET` | `/api/Booking/List` | Get filtered bookings by date range | ✅ Yes |
| `POST` | `/api/Booking/addCustomer` | Create new booking | ✅ Yes |
| `POST` | `/api/Booking/Reports` | Upload diagnostic report file for booking | ✅ Yes |
| `GET` | `/api/Booking/Reports/{bookingId}` | Get list of reports attached to booking | ✅ Yes |
| `DELETE`| `/api/Booking/Reports/{reportId}` | Delete attached clinical report file | ✅ Yes |

### 📊 6. Dashboard Analytics (`/api/Dashboard`)
| Method | Endpoint | Description | Auth Required |
| :--- | :--- | :--- | :---: |
| `GET` | `/api/Dashboard/Stats` | Real-time counts of staff, revenue & bookings | ✅ Yes |
| `GET` | `/api/Dashboard/RecentBookings` | Get latest bookings list | ✅ Yes |

---

## 🧪 Automated Testing

The solution includes an automated **xUnit Integration Test Suite** in `HealthSeva.Tests`.

### Run Test Suite:

From the repository root, execute:

```bash
dotnet test
```

### Test Scope:
- **Authentication & JWT Token Generation**
- **Master Data Lookups**
- **Customer CRUD Lifecycles**
- **Dashboard & Booking Query Validations**
- **Staff Role Dispatch Endpoints**

---

## 📜 License & Notes

- **Environment:** Development & Production ready setup for PostgreSQL.
- **Author:** HealthSeva Core Backend Team.
