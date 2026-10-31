# req006 — Requerimientos: Alta de panel solar

> Identificador: **req006 (alta de panel solar)**. La documentación está en requerimientos/req006_alta_panel_solar y las herramientas en tests/req006. Ver [estructura actual](../../README.md).

## Descripción General

Como Administrador del sistema SolarUY, quiero poder registrar un nuevo panel solar en la plataforma.

## Criterios de Aceptación

1. El sistema debe permitir el ingreso de los siguientes datos básicos del nodo:
   - Nombre (Obligatorio, no se puede repetir en el mismo nodo).
   - Modelo (Obligatorio).
   - PotenciaNominalW (Obligatorio).

   estos datos no se solicitan a la hora de ingresar un nodo:
   - ID (Obligatorio, único en el sistema, autogenerado).
   - FechaInstalacion (fecha actual, guardar hasta los segundos).
   - Estado (al crear el panel el estado queda en Operativo).
   - GeneracionActualW (derivado de la ultima medicion del panel, guardada en la clase "Medicion", de momento no existe la funcionalidad de Medicion).
   - UltimoMantenimiento (derivado de la ultima fecha de mantenimiento del panel, guardada en la clase "Mantenimiento", de momento no existe la funcionalidad de Mantenimiento).
   - IdNodo (id del nodo al que pertenece el panel solar).

2. Seguridad: Solo los usuarios autenticados con el rol de "Administrador" están autorizados a realizar esta acción.
3. Se debe poder agregar paneles desde el boton en el detalle del nodo que dice "+ Agregar Panel Solar"
4. Se debe poder ver una lista de paneles solares pertenecientes al nodo en el detalle del nodo.

## Reglas de Negocio

- cada panel tendra un historial de medicion, con la clase medicion.
- cada panel tendra un historial de mantenimiento, con la clase mantenimiento.
- Estado sera un enum el cual esta detallado en el modelo de dominio. (Operativo, EnFalla, EnMantenimiento, Desactivado)
