# req010 — Requerimientos: Mantenimiento de panel solar

> Identificador: **req010 (Mantenimiento de panel solar)**. La documentación está en requerimientos/req010_mantenimiento_panel_solar y las herramientas en tests/req010. Ver [estructura actual](../../README.md).

## Descripción General

Como Administrador del sistema SolarUY, quiero poder dejar en EnMantenimiento o Desactivado los paneles solares.

## Criterios de Aceptación

1. Modificar el funcionamiento del inversor simulado para que no permita que el estado cambie solo, lo unico que se debe elejir al azar es la potencia que esta generando.
2. Se debe poder poner en mantenimiento un panel solar desde la pantalla de monitoreo, mediante un boton que diga "Iniciar Mantenimiento", una vez se clickee, se debe cortar la conexión tiempo real y la medicion queda en 0W hasta que se termine el mantenimiento, asi tambien como cambiar su estado a En Mantenimiento. Tambien una vez este en mantenimiento se debe poder clickear en el mismo boton, pero con el texto cambiado a "Finalizar Mantenimiento", una vez se clickee, se debe volver con la conexión tiempo real y volver a su estado operativo.
3. Se debe poder desactivar un panel solar desde la pantalla de monitoreo, mediante un boton que diga "Desactivar Panel Solar", una vez se clickee, se debe cortar la conexión tiempo real y la medicion queda en 0W, asi tambien como cambiar su estado a desactivado. Tambien una vez este en estado desactivado se debe poder clickear en el mismo boton, pero con el texto cambiado a "Activar Panel Solar", una vez se clickee, se debe volver con la conexión tiempo real y volver a su estado operativo.
4. Se debe poder setear un panel solar en EnFalla desde la pantalla de monitoreo, mediante un boton que diga "Simular Falla", una vez se clickee, se debe cortar la conexión tiempo real y la medicion queda en 0W, asi tambien como cambiar su estado a En Falla. Tambien una vez este en estado en falla se debe poder clickear en el mismo boton, pero con el texto cambiado a "Simular Arreglo", una vez se clickee, se debe volver con la conexión tiempo real y volver a su estado operativo..
5. Al realizar un mantenimiento se debe registrar lo indicado en el modelo de dominio /.agents/images/Modelo_de_Dominio.png .
6. Se debe controlar los casos excepcionales donde por ejemplo: se clickee en "Iniciar Mantenimiento" y luego a "Desactivar Panel Solar".
7. Seguridad: Solo los usuarios autenticados con el rol de "Administrador" están autorizados a realizar la falla y el desactivar de los paneles, el mantenimiento lo pueden realizar tanto administradores como operadores.
