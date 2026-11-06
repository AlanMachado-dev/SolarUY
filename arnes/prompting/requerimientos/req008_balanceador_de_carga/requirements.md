# req008 — Requerimientos: Balanceo de carga de SolarUY

> Identificador: **req008 (Balanceo de carga de SolarUY)**. La documentación está en requerimientos/req008_balanceador_de_carga y las herramientas en tests/req008. Ver [estructura actual](../../README.md).

## Descripción General

Incorporar un balanceador de carga como punto único de acceso a SolarUY, conforme al Diagrama_de_Arquitectura_General.png de .agents/Images.

El sistema debe poder ejecutarse en varias instancias del servidor, distribuyendo el tráfico entre ellas y manteniendo el funcionamiento de la autenticación, las operaciones existentes y el monitoreo en tiempo real.

## Alcance

La implementación debe permitir ejecutar y verificar, en un entorno local, un balanceador y dos instancias de SolarUY conectadas a una misma base de datos.

## Criterios de Aceptación

1. Distribución de carga. Con al menos dos instancias disponibles, el balanceador debe distribuir nuevas sesiones o conexiones entre ellas. Debe existir una forma de comprobar qué instancia atendió cada solicitud. Las conexiones que necesiten permanecer asociadas a una instancia deben conservar esa asociación.

2. Autenticación y autorización. El inicio y cierre de sesión mediante el proveedor OpenID Connect existente deben funcionar desde la dirección del balanceador. Las instancias deben reconocer una misma sesión autenticada y respetar los permisos de Administrador y Operador.

3. Operaciones y persistencia. Las funcionalidades existentes deben seguir funcionando a través del balanceador. Los datos guardados desde una instancia deben poder consultarse desde otra, utilizando la base de datos compartida.

4. Monitoreo en tiempo real. Las conexiones de SignalR y los componentes interactivos de Blazor deben funcionar a través del balanceador. Una medición recibida y guardada por una instancia debe actualizar las pantallas de los usuarios autorizados que monitoreen ese panel, aunque estén conectados a otra instancia.

5. Instancia fuera de servicio. Al detener una instancia, el balanceador debe detectar su indisponibilidad y dejar de enviarle nuevas solicitudes. Las nuevas conexiones deben poder utilizar una instancia disponible. Si una pantalla interactiva pierde su conexión, debe informar la interrupción y permitir recuperar su funcionamiento mediante reconexión o recarga.

6. Reincorporación. Al restablecer una instancia y comprobar que está disponible, el balanceador debe volver a incluirla en la distribución de tráfico.

7. Verificación reproducible. Documentar cómo iniciar el entorno, comprobar la distribución, detener y reincorporar una instancia, y verificar autenticación, persistencia y monitoreo entre instancias.

## Restricciones

- Respetar los diagramas y las reglas de arquitectura del proyecto.

- Conservar los permisos y las reglas de negocio existentes.
