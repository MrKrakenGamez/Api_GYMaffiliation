# GymAffiliate Manager — API REST (.NET 8)

## Requisitos

| Herramienta | Versión mínima |
|---|---|
| Visual Studio 2022 | 17.8+ |
| .NET SDK | 8.0 |
| SQL Server | 2019+ / Azure SQL |

---

## Estructura del proyecto

```
GymAffiliate/
├── GymAffiliate.sln
└── src/
    ├── Domain/          # Entidades, enums, excepciones, interfaces de repositorios
    ├── Shared/          # Result pattern, ApiResponse, ErrorCodes, Constantes
    ├── Application/     # UseCases, DTOs, Validaciones (FluentValidation), AutoMapper
    ├── Infrastructure/  # Dapper, Repositorios, Configuración, JWT
    └── Api/             # Controllers, Middleware, Program.cs
```

---

## Pasos para ejecutar en VS 2022

### 1. Base de datos
Asegúrate de haber ejecutado los scripts SQL en orden:
```
01_schema.sql
02_infrastructure_tables.sql  (ErrorHandling + SystemLog)
03_sp_affiliates.sql
04_sp_memberships_payments.sql
05_sp_notifications_checkin.sql
06_additional_indexes.sql
seed/05_seed.sql
```

### 2. Connection String
Edita `src/Api/appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=TU_SERVIDOR;Database=GymAffiliateDB;User Id=sa;Password=TU_PASSWORD;TrustServerCertificate=True;"
  }
}
```

O usa **User Secrets** en VS2022 (recomendado):
```
Clic derecho en GymAffiliate.Api → Manage User Secrets
```
```json
{
  "ConnectionStrings:DefaultConnection": "Server=...;Database=GymAffiliateDB;..."
}
```

### 3. Restaurar NuGet y compilar
```
Build → Restore NuGet Packages
Build → Build Solution  (Ctrl+Shift+B)
```

### 4. Ejecutar
```
Seleccionar GymAffiliate.Api como proyecto de inicio
F5 (Debug) o Ctrl+F5 (sin debug)
```

Swagger abrirá automáticamente en: `https://localhost:7001`

---

## Endpoints principales

| Método | Endpoint | Descripción |
|---|---|---|
| POST | `/api/afiliados` | Crear afiliado |
| PUT | `/api/afiliados/{id}` | Actualizar afiliado |
| DELETE | `/api/afiliados/{id}` | Baja lógica |
| GET | `/api/afiliados/{id}` | Obtener con membresía vigente |
| GET | `/api/afiliados` | Listar paginado con filtros |
| POST | `/api/membresias/asignar` | Asignar membresía |
| POST | `/api/membresias/renovar` | Renovar membresía |
| PUT | `/api/membresias/cambiar-plan` | Cambiar tipo de membresía |
| POST | `/api/pagos/registrar` | Registrar pago |
| GET | `/api/pagos/historial/{afiliadoId}` | Historial de pagos |
| GET | `/api/acceso/validar?affiliateId=1` | Validar si puede ingresar |
| POST | `/api/acceso/registrar-ingreso` | Registrar check-in |
| GET | `/api/notificaciones/por-vencer?daysAhead=3` | Membresías por vencer |
| POST | `/api/notificaciones/enviar-alerta` | Generar notificaciones |
| GET | `/api/reportes/ingresos` | Reporte ingresos mensual |
| GET | `/api/reportes/afiliados-activos` | Distribución por estado |
| GET | `/health` | Health check |

---

## Formato de respuestas

**Éxito:**
```json
{
  "success": true,
  "data": { "affiliateId": 1, "message": "Afiliado registrado exitosamente." }
}
```

**Lista paginada:**
```json
{
  "success": true,
  "data": {
    "items": [...],
    "totalCount": 42,
    "page": 1,
    "pageSize": 20,
    "totalPages": 3
  }
}
```

**Error de negocio (SP):**
```json
{
  "success": false,
  "error": {
    "code": "AF_001",
    "message": "El afiliado ya existe con ese documento o email.",
    "status": 409,
    "timestamp": "2026-04-28T10:30:00Z"
  }
}
```

**Error de validación:**
```json
{
  "success": false,
  "error": {
    "code": "VAL_001",
    "message": "Errores de validación.",
    "status": 422,
    "details": {
      "email": ["El email no tiene un formato válido."],
      "birthDate": ["El afiliado debe tener al menos 14 años."]
    }
  }
}
```

---

## Activar JWT (cuando esté listo)

En `appsettings.json`:
```json
"Auth": {
  "UseJwt": true,
  "JwtSettings": {
    "Secret": "minimo_32_caracteres_seguro!!!!!",
    "Issuer": "GymAffiliateAPI",
    "Audience": "GymAffiliateClients",
    "ExpirationMinutes": 60
  }
}
```

Descomentar `[Authorize]` en los controllers según el rol requerido.

---

## Variables de entorno para producción

```bash
ConnectionStrings__DefaultConnection="Server=prod;..."
Auth__JwtSettings__Secret="secreto_produccion_seguro"
Auth__UseJwt="true"
ASPNETCORE_ENVIRONMENT="Production"
```

---

## Arquitectura

```
HTTP Request
    │
    ▼
[Controller]          → Recibe DTO, extrae UserId/IP del HttpContext
    │
    ▼
[UseCase Handler]     → Valida (FluentValidation), mapea parámetros
    │
    ▼
[Repository Interface] → Definida en Domain (inversión de dependencias)
    │
    ▼
[Repository Impl]     → Infrastructure/Dapper — construye DynamicParameters
    │
    ▼
[StoredProcedure]     → SQL Server ejecuta la lógica de negocio
    │
    ▼
[Result<T>]           → Éxito o error tipado (nunca excepciones para flujo normal)
    │
    ▼
HTTP Response         → ApiResponse<T> / PagedApiResponse<T> / error JSON
```

### Mapeo ErrorId → HTTP Status

| ErrorId SP | Code API | HTTP |
|---|---|---|
| 1 | AF_001 | 409 Conflict |
| 2 | AF_002 | 422 Unprocessable |
| 3 | SY_003 | 503 Service Unavailable |
| 4 | AF_004 | 404 Not Found |
| 5 | MB_005 | 403 Forbidden |
| 6 | PA_006 | 409 Conflict |
| 7 | AU_007 | 401 Unauthorized |
| 8 | SU_008 | 404 Not Found |
| 9 | NT_009 | 500 Internal Server Error |
| 10 | MB_010 | 404 Not Found |
