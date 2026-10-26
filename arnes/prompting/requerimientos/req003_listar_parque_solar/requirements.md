# req003 — Requerimientos: Listar Parque Solar

> Identificador: **req003 (Listar Parque Solar)**. La documentación está en requerimientos/req003_Listar_parque_solar y las herramientas en tests/req003. Ver [estructura actual](../../README.md).

## Descripción General
Como Administrador del sistema SolarUY, quiero poder consultar la lista de parques solares, pudiendo ademas clickear en cada uno para ver los detalles del mismo.

## Criterios de Aceptación
1. El sistema debe mostrar los siguientes datos básicos del parque tanto en la lista como en los detalles:
   - ID
   - Nombre
   - Ubicación Localidad / Dirección
   - Capacidad Total Estimada en MW
2. Seguridad: Todos los usuarios una vez que inicien sesion pueden acceder a la lista y los detalles.
3. Redireccion desde el inicio sesion: Una vez se inicie sesion, se debe redirigir al listado de parques solares.