# Documentación de requerimientos y modificaciones

Las rutas de esta página se interpretan desde arnes/prompting. Los comandos de las guías se ejecutan desde la raíz del repositorio, donde está SolarUY.slnx.

## Identificadores y ubicaciones actuales

| Identificador | Tema | Documentación | Pruebas auxiliares |
| --- | --- | --- | --- |
| req001 | Alta de parque solar | [requerimientos/req001_alta_parque_solar](requerimientos/req001_alta_parque_solar) | [tests/req001](tests/req001) |
| mod001 | Cambio de SQLite a MySQL | [modificaciones/mod001_cambio_base_datos](modificaciones/mod001_cambio_base_datos) | [tests/mod001](tests/mod001) |
| req002 | Autenticación y autorización OpenID Connect | [requerimientos/req002_implementacion_openID](requerimientos/req002_implementacion_openID) | [tests/req002](tests/req002) |

Cada carpeta de documentación conserva requirements.md, specs.md y tasks.md. Las guías y resultados pertenecen a su requerimiento o modificación; los scripts están separados bajo tests.

## Guías vigentes

- [Conexión MySQL y arranque — mod001](modificaciones/mod001_cambio_base_datos/configuracion.md).
- [Detalles técnicos de MySQL — mod001](modificaciones/mod001_cambio_base_datos/referencia_tecnica.md).
- [Configuración Auth0, inicio y cierre — req002](requerimientos/req002_implementacion_openID/configuracion.md).
- [Aceptación OIDC y pendientes reales — req002](requerimientos/req002_implementacion_openID/aceptacion.md).

La [guía original de req001](requerimientos/req001_alta_parque_solar/configuracion.md) es histórica: SQLite y contraseñas locales fueron sustituidos. Los scripts de tests/req001 y la parte de autenticación local de tests/mod001 tampoco validan el acceso OIDC vigente.

## Equivalencias históricas

| Nombre anterior | Identificador y ubicación vigentes | Evidencias originales |
| --- | --- | --- |
| req001_alta_parque_solar | requerimientos/req001_alta_parque_solar | artifacts/req001 |
| req002_cambio_base_datos | modificaciones/mod001_cambio_base_datos | artifacts/req002 |
| req003_implementacion_openID | requerimientos/req002_implementacion_openID | artifacts/req003 |

Las rutas artifacts/... se interpretan desde la raíz del repositorio. No se renombran evidencias, esquemas de prueba, marcadores de proceso ni prefijos técnicos: forman parte del registro verificable de las ejecuciones originales. Por eso artifacts/req002 corresponde a MySQL, aunque el requerimiento vigente req002 sea OpenID Connect.

Los apartados de registro de ejecución mantienen los identificadores, fechas, resultados y rutas originales, identificados como históricos. Las ubicaciones operativas, comandos y enlaces se actualizaron tras reorganizar la documentación el 22/10/2026. Esta revisión no repite ni altera resultados de aceptación ni implica que los hashes históricos de documentos deban coincidir con sus ediciones actuales.

## Herramientas y controles

- [Controles HTTP locales OIDC](tests/req002/verificar_controles_locales.py).
- [Preparación y limpieza de aceptación real](tests/req002/gestionar_aceptacion_real.ps1).
- [Proxy funcional OIDC](tests/req002/ProxyAceptacion.cs).
- [Arnés histórico MySQL](tests/mod001/verificar_aceptacion.py) y [auxiliar de persistencia](tests/mod001/verificar_persistencia.cs).
- [Registro histórico de ajustes de arquitectura](ajustes_arnes_propuestos.md).

Las reglas compartidas y los comandos de compilación, Domain y ArchUnitNET están en [AGENTS.md](../../AGENTS.md). Las herramientas funcionales complementan esos controles. No ejecutar las que escriben datos contra una base habitual.
