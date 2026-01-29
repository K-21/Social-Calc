# Social Calc - Web-Based Spreadsheet Application

A full-featured web-based spreadsheet application built with **ASP.NET Core** and **SocialCalc** JavaScript library. Supports Excel import/export, multi-sheet workbooks, formulas, and collaborative features.

![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![PHP](https://img.shields.io/badge/PHP-8.0+-blue)
![License](https://img.shields.io/badge/License-MIT-green)

---

## 📋 Table of Contents

- [Features](#-features)
- [Prerequisites](#-prerequisites)
- [Installation](#-installation)
- [Configuration](#-configuration)
- [Database Setup](#-database-setup)
- [PHP Setup (Excel Import/Export)](#-php-setup-excel-importexport)
- [Running the Application](#-running-the-application)
- [Project Structure](#-project-structure)
- [API Endpoints](#-api-endpoints)
- [Troubleshooting](#-troubleshooting)
- [Contributing](#-contributing)

---

## ✨ Features

- **📊 Full Spreadsheet Functionality** - Cell editing, formulas, formatting
- **📁 Multi-Sheet Workbooks** - Create and manage multiple sheets
- **📥 Import/Export** - Support for Excel (.xlsx), CSV, ODS, HTML, PDF
- **💾 Auto-Save** - Automatic saving with debounced commits
- **🔐 User Authentication** - Register, login, password reset
- **📱 Responsive Design** - Works on desktop and mobile

---

## 📌 Prerequisites

Before you begin, ensure you have the following installed:

| Software | Version | Download |
|----------|---------|----------|
| **.NET SDK** | 10.0+ | [Download](https://dotnet.microsoft.com/download) |
| **SQL Server** | LocalDB or Express | [Download](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) |
| **PHP** | 8.0+ | [Download](https://www.php.net/downloads) |
| **Composer** | Latest | [Download](https://getcomposer.org/download/) |
| **Git** | Latest | [Download](https://git-scm.com/downloads) |

### Verify Installations

```bash
dotnet --version    # Should show 10.0.x or higher
php -v              # Should show PHP 8.x
composer -V         # Should show Composer version
```

---

## 🚀 Installation

### 1. Clone the Repository

```bash
git clone https://github.com/YOUR_USERNAME/Social-Calc.git
cd Social-Calc
```

### 2. Restore .NET Dependencies

```bash
dotnet restore
```

### 3. Install PHP Dependencies

```bash
cd excelinterop
composer install
cd ..
```

### 4. Create Configuration File

Copy the template configuration and update with your settings:

```bash
# Windows (PowerShell)
Copy-Item appsettings.template.json appsettings.json

# Linux/Mac
cp appsettings.template.json appsettings.json
```

---

## ⚙️ Configuration

Edit `appsettings.json` with your specific settings:

### Database Connection

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=social_calc;Trusted_Connection=true;"
}
```

**Connection String Examples:**

| Database Type | Connection String |
|--------------|-------------------|
| LocalDB | `Server=(localdb)\MSSQLLocalDB;Database=social_calc;Trusted_Connection=true;` |
| SQL Server Express | `Server=.\SQLEXPRESS;Database=social_calc;Trusted_Connection=true;` |
| SQL Server (Auth) | `Server=your-server;Database=social_calc;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=true;` |

### Security Keys

Generate secure random keys for production:

```json
"AppSettings": {
  "SecretKey": "YOUR_32_CHARACTER_RANDOM_KEY_HERE",
  "JwtSecret": "YOUR_64_CHARACTER_RANDOM_JWT_SECRET"
}
```

**Generate secure keys (PowerShell):**
```powershell
# 32-character key
-join ((65..90) + (97..122) + (48..57) | Get-Random -Count 32 | ForEach-Object {[char]$_})

# 64-character key  
-join ((65..90) + (97..122) + (48..57) | Get-Random -Count 64 | ForEach-Object {[char]$_})
```

### PHP Configuration

```json
"AppSettings": {
  "UsePhpCli": true,
  "PhpCliPath": "php",
  "PhpScriptsDir": "excelinterop",
  "PhpTempDir": "excelinterop/tmp"
}
```

> **Note:** Use absolute paths if PHP is not in your system PATH.

### Email Configuration (Optional)

```json
"Email": {
  "From": "noreply@yourdomain.com",
  "SmtpServer": "smtp.gmail.com",
  "SmtpPort": 587,
  "UseCredentials": true,
  "Username": "your-email@gmail.com",
  "Password": "your-app-password"
}
```

---

## 🗄️ Database Setup

### Option 1: Using Entity Framework Migrations (Recommended)

```bash
# Apply migrations to create database schema
dotnet ef database update
```

### Option 2: Manual Setup

If migrations don't exist, create the database manually:

```sql
-- Connect to SQL Server and run:
CREATE DATABASE social_calc;
```

Then run the application - Entity Framework will create tables automatically.

### Verify Database

```bash
# Check if database exists (SQL Server)
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "SELECT name FROM sys.databases WHERE name = 'social_calc'"
```

---

## 🐘 PHP Setup (Excel Import/Export)

PHP is required for Excel, CSV, ODS, HTML, and PDF import/export functionality.

### 1. Install PHP

**Windows:**
- Download PHP from [windows.php.net](https://windows.php.net/download/)
- Extract to `C:\php`
- Add `C:\php` to your system PATH
- Enable extensions in `php.ini`: `extension=zip`, `extension=gd`, `extension=mbstring`, `extension=xml`

**Linux (Ubuntu/Debian):**
```bash
sudo apt update
sudo apt install php php-cli php-xml php-zip php-gd php-mbstring
```

**macOS:**
```bash
brew install php
```

### 2. Install Composer Dependencies

```bash
cd excelinterop
composer install
```

This installs:
- PhpSpreadsheet (Excel read/write)
- TCPDF (PDF export)

### 3. Verify PHP Setup

```bash
# Test PHP
php -v

# Test export functionality
echo '{"numsheets":1,"currentid":"sheet1","sheetArr":{"sheet1":{"sheetstr":{"savestr":"version:1.5\ncell:A1:t:Test\n"},"name":"Sheet1"}}}' > test.json
php excelinterop/export.php test.json output.xlsx Xlsx
```

### 4. Create Temp Directory

```bash
mkdir -p excelinterop/tmp
```

---

## ▶️ Running the Application

### Development Mode

```bash
dotnet run
```

The application will start at: **http://localhost:5000**

### Production Mode

```bash
dotnet run --configuration Release
```

### Using Visual Studio

1. Open `SocialCalc.Web.csproj` in Visual Studio
2. Press `F5` to run with debugging

---

## 📁 Project Structure

```
SocialCalc-Website/
├── Controllers/           # MVC Controllers
│   ├── AuthController.cs      # Authentication (login, register, password reset)
│   ├── HomeController.cs      # Home page, dashboard
│   └── SheetsController.cs    # Spreadsheet CRUD, import/export
├── Data/                  # Database context
├── excelinterop/          # PHP scripts for Excel processing
│   ├── export.php            # Export to xlsx, csv, ods, html, pdf
│   ├── import.php            # Import from Excel files
│   ├── vendor/               # Composer dependencies (gitignored)
│   └── tmp/                  # Temporary files (gitignored)
├── Migrations/            # EF Core migrations
├── Models/                # Data models (User, Sheet)
├── Services/              # Business logic services
├── Views/                 # Razor views
├── wwwroot/               # Static files (JS, CSS, images)
│   ├── js/                   # SocialCalc JavaScript libraries
│   └── css/                  # Stylesheets
├── appsettings.json       # Configuration (gitignored - create from template)
├── appsettings.template.json  # Configuration template (committed)
└── Program.cs             # Application entry point
```

---

## 🔌 API Endpoints

### Authentication

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/login` | Login page |
| POST | `/login` | Authenticate user |
| GET | `/register` | Registration page |
| POST | `/register` | Create new account |
| POST | `/logout` | Logout user |
| GET | `/lostpw` | Password reset page |

### Sheets

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/Sheets` | List user's sheets |
| GET | `/Sheets/Edit/{id}` | Open sheet editor |
| POST | `/Sheets/Save/{id}` | Save sheet data |
| POST | `/Sheets/Create` | Create new sheet |
| POST | `/Sheets/Delete/{id}` | Delete sheet |

### Import/Export

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/Sheets/Import` | Import Excel/CSV file |
| GET | `/Sheets/Export/{id}` | Export to Excel (.xlsx) |
| GET | `/Sheets/Export-Csv/{id}` | Export to CSV |
| GET | `/Sheets/Export-Format/{id}/{format}` | Export to ods, html |
| GET | `/Sheets/Export-Pdf/{id}` | Export to PDF |

---

## 🔧 Troubleshooting

### Common Issues

#### 1. Database Connection Failed

```
Error: Cannot open database "social_calc" requested by the login
```

**Solution:** Create the database first:
```bash
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "CREATE DATABASE social_calc"
dotnet ef database update
```

#### 2. PHP Not Found

```
Error: Failed to start PHP process for export
```

**Solution:** 
- Install PHP 8.0+
- Add PHP to system PATH
- Or use absolute path in `appsettings.json`:
```json
"PhpCliPath": "C:\\php\\php.exe"
```

#### 3. Export Creates Blank File

```
Error: JSON decode error
```

**Solution:** Ensure the sheet has been saved at least once before exporting. Click the Save button before exporting.

#### 4. Composer Dependencies Missing

```
Error: Class 'PhpOffice\PhpSpreadsheet\Spreadsheet' not found
```

**Solution:**
```bash
cd excelinterop
composer install
```

#### 5. PDF Export Not Working

```
Error: No writer found for type Pdf
```

**Solution:** Install TCPDF:
```bash
cd excelinterop
composer require tecnickcom/tcpdf
```

---

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Commit your changes: `git commit -m 'Add amazing feature'`
4. Push to branch: `git push origin feature/amazing-feature`
5. Open a Pull Request

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- [SocialCalc](https://github.com/nickshearer/socialcalc) - JavaScript spreadsheet engine
- [PhpSpreadsheet](https://github.com/PHPOffice/PhpSpreadsheet) - PHP Excel library
- [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/) - Web framework
- [TCPDF](https://tcpdf.org/) - PDF generation library
