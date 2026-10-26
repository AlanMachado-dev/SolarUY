# req004 — Requerimientos: Alta de Nodo

> Identificador: **req004 (alta de nodo)**. La documentación está en requerimientos/req004_alta_nodo y las herramientas en tests/req004. Ver [estructura actual](../../README.md).

## Descripción General

Como Administrador del sistema SolarUY, quiero poder registrar un nuevo Nodo en la plataforma para luego poder gestionar independiente su inversor y sensor, asi tambien como los paneles del mismo cuando este presente la funcionalidad de crear paneles.

## Criterios de Aceptación

1. El sistema debe permitir el ingreso de los siguientes datos básicos del nodo:
   - Nombre (Obligatorio).
   - Latitud (Obligatorio).
   - Longitud (Obligatorio).

   estos datos no se solicitan a la hora de ingresar un nodo:
   - ID (Obligatorio, único en el sistema, autogenerado).
   - IdParque (id del parque al que pertenece el nodo).
   - CantidadPaneles (calculado mediante la suma de paneles pertenecientes al nodo).
   - IdInversor (id del inversor perteneciente a este nodo).

2. El sistema debe permitir el ingreso de los siguientes datos básicos del inversor en el menu de agregar nodo:
   - Nombre (Obligatorio).
   - NumeroSerie (Obligatorio).

   estos datos no se solicitan a la hora de ingresar un inversor:
   - IdNodo (id del nodo al que pertenece el inversor).
   - IdInversor (único en el sistema, autogenerado).
   - IdSensor (id del sensor perteneciente al inversor).

3. El sistema debe permitir el ingreso de los siguientes datos básicos del Sensor en el menu de agregar nodo:
   - Nombre (Obligatorio).
   - NumeroSerie (Obligatorio).
   - Endpoint (Opcional).

   estos datos no se solicitan a la hora de ingresar un Sensor:
   - IdInversor (id del Inversor al que pertenece el Sensor).
   - IdSensor (único en el sistema, autogenerado).

4. Seguridad: Solo los usuarios autenticados con el rol de "Administrador" están autorizados a realizar esta acción.
5. Se debe poder agregar nodos desde un boton en el detalle del parque solar que diga "+ Agregar nodo", dentro de ese mismo menu se debe poder agregar tanto el nodo como el inversor y el sensor, claramente separado e indicado el ingreso de cada uno.

## Reglas de Negocio

- Se pueden crear nodos sin paneles solares asignados.
- No se pueden crear nodos sin inversor y sin sensor.
- El parque solar debe tener una lista de nodos guardados por su ID.
- Se pueden agregar los nodos que se quieran a un mismo parque.
- Solo puede haber un inversor por nodo.
- Solo puede haber un sensor por inversor.
