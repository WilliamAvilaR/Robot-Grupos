# Instrucciones para Probar el Login

## Configuración Previa

1. **Configura tus credenciales** en `src/Scraper.Worker/appsettings.json`:

```json
{
  "Facebook": {
    "Email": "tu_email@ejemplo.com",
    "Password": "tu_contraseña"
  }
}
```

2. **Asegúrate de que el navegador esté instalado**:

```bash
cd src/Scraper.Worker
dotnet build
pwsh bin/Debug/net9.0/playwright.ps1 install chromium
```

## Ejecutar la Prueba de Login

```bash
cd src/Scraper.Worker
dotnet run
```

## Qué Esperar

1. **El navegador se abrirá** (no está en modo headless para que puedas verlo)
2. **Navegará a Facebook** automáticamente
3. **Detectará si necesitas login**:
   - Si ya estás autenticado: mostrará mensaje de éxito
   - Si necesita login: ejecutará el login automáticamente
   - Si requiere OTP: te mostrará instrucciones para insertarlo

4. **Después del login exitoso**:
   - El navegador permanecerá abierto
   - Verás la sesión guardada en `C:\ScraperData\state\default\storageState.json`
   - El worker esperará hasta que presiones Ctrl+C

## Si Requiere OTP

Si Facebook solicita código de verificación:

1. Verás en los logs el `ChallengeId` (GUID)
2. Ejecuta este SQL para insertar el código:

```sql
UPDATE OtpChallenge 
SET Status = 'Submitted', Code = 'TU_CODIGO_AQUI' 
WHERE Id = 'GUID_DEL_CHALLENGE';
```

3. El sistema detectará el código automáticamente y continuará

## Verificar el Estado de Autenticación

Puedes verificar si el login fue exitoso revisando:

- **Logs en consola**: Busca "Login exitoso" o "Usuario ya autenticado"
- **Archivo de sesión**: `C:\ScraperData\state\default\storageState.json` (debe existir después del login)
- **Navegador**: Debe mostrar tu feed de Facebook

## Troubleshooting

### Error: "Las credenciales de Facebook no están configuradas"
- Verifica que `appsettings.json` tenga `Facebook:Email` y `Facebook:Password`

### Error: "Login fallido"
- Verifica que las credenciales sean correctas
- Facebook puede requerir verificación adicional (OTP)
- Revisa los logs para más detalles

### El navegador no se abre
- Verifica que Playwright esté instalado: `pwsh bin/Debug/net9.0/playwright.ps1 install chromium`
- Verifica que `Headless: false` en `appsettings.json`

### Error de conexión a base de datos
- Verifica que la connection string en `appsettings.json` sea correcta
- Asegúrate de que SQL Server LocalDB esté corriendo
