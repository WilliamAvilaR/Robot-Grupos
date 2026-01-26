# Instrucciones para Aplicar Migraciones

## Opción 1: Usando dotnet ef (Recomendado)

### 1. Aplicar la migración a la base de datos existente

```bash
dotnet ef database update --project src/Scraper.Infrastructure --startup-project src/Scraper.Worker --context ScraperDbContext
```

Este comando:
- ✅ Aplicará solo las nuevas tablas (`OtpChallenge` y `ScrapeResult`)
- ✅ No afectará las tablas existentes de tu otro proyecto
- ✅ Es seguro ejecutarlo en una base de datos con datos existentes

### 2. Verificar que las tablas se crearon

```sql
USE [DataColorApp]
GO

SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME IN ('OtpChallenge', 'ScrapeResult')
```

## Opción 2: Script SQL Manual

Si prefieres aplicar manualmente, puedes generar el script SQL:

```bash
dotnet ef migrations script --project src/Scraper.Infrastructure --startup-project src/Scraper.Worker --context ScraperDbContext --output migration.sql
```

Luego ejecuta el archivo `migration.sql` en SQL Server Management Studio.

## Opción 3: Revertir la migración (si es necesario)

Si necesitas revertir la migración:

```bash
dotnet ef database update 0 --project src/Scraper.Infrastructure --startup-project src/Scraper.Worker --context ScraperDbContext
```

## Verificar la Migración

Después de aplicar, verifica que las tablas existen:

```sql
USE [DataColorApp]
GO

-- Ver estructura de OtpChallenge
SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'OtpChallenge'

-- Ver estructura de ScrapeResult
SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ScrapeResult'
```

## Notas Importantes

- ✅ Las migraciones de EF Core solo crean/modifican las tablas definidas en `ScraperDbContext`
- ✅ No afectará otras tablas en la base de datos `DataColorApp`
- ✅ Puedes tener múltiples `DbContext` en la misma base de datos sin problemas
- ✅ Si ya ejecutaste el script SQL manual (`database/Schema.sql`), la migración detectará que las tablas existen y no las recreará

## Troubleshooting

Si encuentras errores:

1. **Error de conexión**: Verifica que la connection string en `appsettings.json` sea correcta
2. **Tablas ya existen**: Si ya creaste las tablas manualmente, EF Core puede fallar. En ese caso:
   - Opción A: Elimina las tablas manualmente y ejecuta la migración
   - Opción B: Marca la migración como aplicada: `dotnet ef database update --project src/Scraper.Infrastructure --startup-project src/Scraper.Worker --context ScraperDbContext --connection "Server=(localdb)\MSSQLLocalDB;Database=DataColorApp;Integrated Security=true"`
