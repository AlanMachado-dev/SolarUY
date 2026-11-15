# req012 — Requerimientos: Mantenimiento de panel solar

> Identificador: **req012 (Mantenimiento de panel solar)**. La documentación está en requerimientos/req012_mantenimiento_panel_solar y las herramientas en tests/req012. Ver [estructura actual](../../README.md).

## Descripción General

Como Administrador del sistema SolarUY, quiero poder dejar en EnMantenimiento o Desactivado los paneles solares.

## Criterios de Aceptación

1. Modificar el funcionamiento del inversor simulado para que no permita que el estado cambie solo, lo unico que se debe elejir al azar es la potencia que esta generando.
2. Se debe poder poner en mantenimiento un panel solar desde la pantalla de monitoreo, mediante un boton que diga "Programar Mantenimiento", una vez se llegue a la fecha y hora programada del mantenimiento, se debe cortar la conexión en tiempo real y la medicion queda en 0W hasta que se termine el mantenimiento, asi tambien como cambiar su estado a En Mantenimiento. Tambien una vez este en mantenimiento se debe poder clickear en el mismo boton, pero con el texto cambiado a "Finalizar Mantenimiento", una vez se finalice el mantenimiento, se debe volver con la conexión en tiempo real y volver a su estado operativo.
3. Se debe poder setear un panel solar en EnFalla desde la pantalla de monitoreo, mediante un boton que diga "Simular Falla", una vez se clickee, se debe cortar la conexión en tiempo real y la medicion queda en 0W, asi tambien como cambiar su estado a En Falla. Tambien una vez este en estado en falla se debe poder clickear en el mismo boton, pero con el texto cambiado a "Simular Arreglo", una vez se clickee, se debe volver con la conexión en tiempo real y volver a su estado operativo.
4. Al realizar un mantenimiento se debe registrar lo indicado en el modelo de dominio /.agents/images/Modelo_de_Dominio.png, para eso al clickear en "Programar Mantenimiento" se debe desplegar un modal para ingresar los siguientes datos:
    - Tipo (Obligatorio).
    - Fecha Programada (Obligatorio).
    - Responsable (Obligatorio).

    estos datos no se solicitan a la hora de programar un mantenimiento:
   - ID (Obligatorio, único en el sistema, autogenerado).

5. Al clickear en "Finalizar Mantenimiento" se debe desplegar un modal para ingresar los siguientes datos:
    - Observaciones (Opcional).

    estos datos no se solicitan a la hora de finalizar un mantenimiento:
    - Fecha Realizada (Obligatorio, a partir de la fecha y hora actual).
6. Se debe controlar TODOS los casos excepcionales donde por ejemplo: se clickee en "Simular falla" y luego a "Desactivar Panel", si se pasa algo asi el boton deberia volver a "Simular Falla", el "Desactivar Panel" pasa a "Activar panel" y el estado queda en el que diga el ultimo boton presionado.
7. Se debe controlar el caso de que este en mantenimiento el panel y se clickee un boton, ya sea el "Simular falla" o el "Desactivar Panel", si el panel esta en mantenimiento en donde el estado va a cambiar a En Falla o Desactivado, pero si se arregla la falla o se vuelve a activar el panel, deberia seguir mostrando en mantenimiento el estado si es que el mantenimiento aun no ha terminado y seguir funcionando como cuando esta en mantenimiento, o sea, sin conexion real y sin mediciones nuevas.
8. Seguridad: Solo los usuarios autenticados con el rol de "Administrador" están autorizados a realizar la falla de los paneles, el mantenimiento lo pueden realizar tanto administradores como operadores.
