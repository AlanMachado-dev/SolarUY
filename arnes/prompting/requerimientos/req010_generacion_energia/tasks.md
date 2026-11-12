# req010 - Tareas de implementación de generación de energía

Fuentes de alcance [requirements.md](requirements.md) y [specs.md](specs.md). Marcar cada casilla después de verificarla.

## 1. Domain

- [x] Implementar el cálculo por panel a partir de pares de lecturas, con el límite de 40 segundos, el recorte al día consultado y la distinción entre cero, falta de datos y cobertura parcial.
- [x] Crear pruebas unitarias xUnit para una potencia constante, potencia cero, ausencia de lecturas, lectura aislada, fechas repetidas, cruce de medianoche, un tramo de exactamente 40 segundos, un hueco mayor y el tratamiento del final del período.
- [x] Ejecutar los controles de salida. Compilación correcta, Domain 103/103 y ArchUnitNET 17/17. La compilación informó advertencias NU1903 de la dependencia existente `Microsoft.AspNetCore.Components.WebAssembly.Server`.

## 2. Application

- [x] Definir `IEnergiaRepository` y el DTO de resultado con período, energía opcional y cobertura.
- [x] Implementar el servicio que verifica el rol, obtiene un único corte con `TimeProvider` y determina el inicio del día en `America/Montevideo`.
- [x] Calcular una estimación por panel y agregar los resultados del nodo y del parque. Resolver por separado los casos sin paneles y sin tramos válidos.
- [x] Ejecutar los controles de salida con el contrato y el servicio compilados. Compilación correcta, Domain 103/103 y ArchUnitNET 17/17 mediante xUnit en proceso.

## 3. Infrastructure

- [x] Implementar `EnergiaRepository` con la jerarquía existente y consultas de solo lectura en lote para paneles y mediciones.
- [x] Aplicar el intervalo UTC con margen anterior a medianoche, el orden estable y la exclusión de lecturas posteriores al corte.
- [x] Registrar el repositorio y el servicio en la composición de la aplicación.
- [x] Ejecutar los controles de salida. Compilación correcta, Domain 103/103 y ArchUnitNET 17/17 mediante xUnit en proceso.

## 4. Presentation

- [x] Mostrar el indicador en el detalle del parque para Administrador y Operador.
- [x] Mostrar el indicador encima del listado de paneles en el detalle del nodo.
- [x] Mostrar el indicador junto al medidor en el monitoreo individual para ambos roles.
- [x] Presentar los kWh, la hora de consulta y los estados «Cobertura parcial», «Sin datos suficientes» y «Sin paneles» según el DTO. Verificar que un valor positivo pequeño se distingue de cero.
- [x] Ejecutar los controles de salida. Compilación correcta, Domain 103/103 y ArchUnitNET 17/17 mediante xUnit en proceso.

## Aceptación funcional

- [x] Preparar datos conocidos en una base aislada. Con el mismo corte y las mismas lecturas, verificar el valor de cada panel, la suma por nodo y la suma del parque. Incluir un panel de otro parque para comprobar el alcance.
- [x] Comprobar el cero medido, un panel sin lecturas, un hueco que produce un subtotal parcial y un nodo o parque vacío.
- [ ] Verificar que Administrador y Operador ven el indicador y que otro usuario autenticado puede abrir el detalle general del parque sin acceder a la estimación.
- [ ] Confirmar que al recargar se consulta un nuevo corte y que el monitoreo individual sigue mostrando la potencia en vivo en W.

La verificación auxiliar aplicó las migraciones en MySQL 8.4.9 aislado y confirmó subtotales de 0,002 y 0,003 kWh y un total de 0,005 kWh para el parque. El indicador se renderizó con datos persistidos, roles de prueba y un nuevo corte.

El 12/11/2026, el usuario confirmó el recorrido en navegador con Administrador y la base local `solaruy`. Tras generar lecturas y recargar el monitoreo, el indicador se mostró en el panel, el nodo y el parque. Queda pendiente la comprobación en navegador con Operador y un usuario sin esos roles, así como la verificación explícita de la hora de corte y del medidor de potencia en vivo.

## Controles de salida por capa

Ejecutar desde la raíz y continuar solo cuando pasen los tres controles.

```powershell
dotnet build SolarUY.slnx
dotnet test tests/SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test arnes/ValidacionDeCodigo/CodeQuality.Tests/CodeQuality.Tests.csproj
```

Registrar los resultados y distinguir advertencias preexistentes de fallos nuevos. Si se crean scripts auxiliares, guardarlos en `arnes/prompting/tests/req010/`. Guardar evidencias depuradas en `artifacts/`.
