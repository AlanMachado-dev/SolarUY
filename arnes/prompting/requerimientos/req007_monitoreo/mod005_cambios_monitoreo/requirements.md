# mod005 — Modificaciones: Cambiar detalles sobre monitoreo en tiempo real de paneles

## Descripción General

Actualizar como se ve el monitoreo en tiempo real la generación de corriente y el estado de los paneles, como también corregir listado en listado de paneles.

## Criterios de Aceptación

1. Al entrar al detalle del nodo, en el listado de paneles que contiene debe mostrar la ultima medición disponible de cada panel, ya que actualmente solo muestra "Sin mediciones".
2. En la pantalla de monitoreo del panel, para la "Generación actual" en lugar de mostrar el número sencillo debe usar un componente Radzen Blazor, por ejemplo el medidor radial.
3. Cambiar tiempo entre mediciones dentro del monitoreo de 30 segundos a 20 segundos.
4. Agregar (si no existe) mención de inversor simulado en algún documento, con explicación de funcionamiento. Actualizar README general.


## Reglas de Negocio

- Dejar documentación consistente si es necesario.
