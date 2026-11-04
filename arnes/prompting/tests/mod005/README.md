# Verificación de monitoreo mod005

Desde la raíz, ejecutar la comprobación de persistencia con la configuración MySQL habitual. Crea una base temporal propia con prefijo `solaruy_mod005_` y la retira al terminar.

```powershell
dotnet run --project arnes/prompting/tests/mod005/VerificarMediciones.csproj --artifacts-path artifacts/mod005/integracion
```

Para la aceptación en el navegador, compilar la aplicación y agregar `-- --servir` al comando anterior.

```powershell
dotnet build SolarUY.slnx --artifacts-path artifacts/mod005/compilacion
dotnet run --project arnes/prompting/tests/mod005/VerificarMediciones.csproj --artifacts-path artifacts/mod005/integracion -- --servir
```

Abrir las rutas impresas por el programa en una sesión autenticada de SolarUY en `localhost`. La instancia de prueba escucha en el puerto 8185. Si el navegador requiere iniciar sesión en ese puerto, la configuración de Auth0 debe permitir la URL de retorno correspondiente.

- En el detalle, comprobar que Cero muestra 0 W, Vacío muestra «Sin mediciones» y Empate muestra la lectura elegida por el desempate.
- Abrir Vacío para comprobar «No disponible» y Empate para comprobar la lectura inicial superior a 500 W. En una sesión Administrador, dejar Empate abierto durante al menos dos envíos. Comprobar los cambios del medidor y su fecha sin recargar.
- Comprobar el acceso a la misma ruta como Administrador, Operador y usuario autenticado sin esos roles. Realizar la verificación de lectura ausente antes de dejar que el sensor registre en Vacío.
- Presionar Enter al terminar. El programa cierra la instancia, muestra los intervalos entre guardados del panel Empate y retira la base temporal.
