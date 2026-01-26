# Robot Social Media - Scraper MVP

Scraper de Facebook usando Playwright con soporte para autenticación y OTP manual.

## Estructura del Proyecto

```
Robotsocialmedia/
├── src/
│   ├── Scraper.Worker/          # Worker Service principal
│   ├── Scraper.Core/            # Interfaces y modelos compartidos
│   ├── Scraper.Orchestrator/    # Casos de uso y coordinación
│   ├── Scraper.Browser/         # Adapter de Playwright
│   ├── Scraper.Auth/            # Gestión de autenticación
│   ├── Scraper.Extraction/      # Pipeline de extracción
│   └── Scraper.Infrastructure/  # Repositorios, DB, etc.
├── database/
│   └── Schema.sql               # Esquema de base de datos
└── Robotsocialmedia.sln
```

## Requisitos Previos

1. .NET 9 SDK
2. SQL Server LocalDB (incluido con Visual Studio)
3. Playwright (se instala automáticamente con `dotnet build`)

## Configuración Inicial

### 1. Aplicar Migraciones a la Base de Datos Existente

Como ya tienes la base de datos `DataColorApp` con tablas de otro proyecto, puedes agregar las nuevas tablas usando migraciones de Entity Framework Core:

```bash
dotnet ef database update --project src/Scraper.Infrastructure --startup-project src/Scraper.Worker --context ScraperDbContext
```

**Nota**: Esta migración solo creará las tablas `OtpChallenge` y `ScrapeResult`. No afectará tus tablas existentes.

**Alternativa**: Si prefieres aplicar manualmente, puedes usar el script SQL en `database/Schema.sql` o generar el script desde la migración:

```bash
dotnet ef migrations script --project src/Scraper.Infrastructure --startup-project src/Scraper.Worker --context ScraperDbContext --output migration.sql
```

Ver `MIGRATION_INSTRUCTIONS.md` para más detalles.

### 2. Instalar Navegadores de Playwright

```bash
pwsh bin/Debug/net9.0/playwright.ps1 install chromium
```

O desde el proyecto:
```bash
cd src/Scraper.Worker
dotnet build
pwsh bin/Debug/net9.0/playwright.ps1 install chromium
```

### 3. Configurar appsettings.json

Edita `src/Scraper.Worker/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DataColor": "Server=(localdb)\\MSSQLLocalDB;Database=DataColorApp;Integrated Security=true"
  },
  "Facebook": {
    "Email": "tu_email@ejemplo.com",
    "Password": "tu_contraseña"
  },
  "Scraper": {
    "AccountId": "default",
    "Urls": [
      "https://www.facebook.com"
    ]
  }
}
```

## Uso

### Ejecutar el Worker

```bash
cd src/Scraper.Worker
dotnet run
```

### Flujo de OTP Manual

Cuando Facebook solicite un código OTP:

1. El sistema creará un registro en la tabla `OtpChallenge` con estado `Pending`
2. Verás en los logs el `ChallengeId` (GUID)
3. Inserta el código OTP manualmente en la base de datos:

```sql
UPDATE OtpChallenge 
SET Status = 'Submitted', Code = 'TU_CODIGO_AQUI' 
WHERE Id = 'GUID_DEL_CHALLENGE';
```

4. El sistema detectará el cambio y continuará automáticamente

### Ejemplo de Consulta para Obtener el ChallengeId Pendiente

```sql
SELECT TOP 1 Id, AccountId, CreatedAt, ExpiresAt
FROM OtpChallenge
WHERE Status = 'Pending' AND ExpiresAt > GETUTCDATE()
ORDER BY CreatedAt DESC;
```

## Arquitectura

### Capas

1. **Worker Service**: Punto de entrada, configura DI y logging
2. **Orchestrator**: Coordina el flujo completo de scraping
3. **Browser Layer**: Adapter de Playwright para aislar la automatización
4. **Auth Layer**: Maneja autenticación, login y OTP
5. **Extraction Layer**: Extrae datos de las páginas
6. **Infrastructure**: Repositorios para BD

### Flujo de Ejecución

1. Worker inicia y ejecuta `JobRunner`
2. `JobRunner` llama a `ScraperOrchestrator.RunScrapeJobAsync`
3. Orchestrator verifica autenticación (`EnsureAuthenticatedAsync`)
4. Si requiere OTP, crea challenge y espera código en BD
5. Una vez autenticado, navega a las URLs configuradas
6. Extrae datos usando `FacebookPageExtractor`
7. Guarda resultados en `ScrapeResult`

## Almacenamiento de Sesión

Los estados de sesión se guardan en:
- Windows: `C:\ScraperData\state\{accountId}\storageState.json`

Esto permite mantener la sesión entre ejecuciones.

## Logs

Los logs se guardan en:
- Consola (formato estructurado)
- Archivo: `logs/scraper-YYYYMMDD.log`

## Próximos Pasos

- [ ] Implementar retry policies avanzadas
- [ ] Sistema de alertas completo
- [ ] Artifacts store (screenshots opcionales)
- [ ] Cola de mensajes para escalar
- [ ] Endpoint admin para insertar OTP vía API
