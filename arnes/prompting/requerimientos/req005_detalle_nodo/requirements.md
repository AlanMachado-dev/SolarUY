# req005 — Requerimientos: Detalle Nodo

> Identificador: **req005 (Detalle Nodo)**. La documentación está en requerimientos/req005_detalle_nodo y las herramientas en tests/req005. Ver [estructura actual](../../README.md).

## Descripción General

Como Administrador del sistema SolarUY, quiero poder consultar los detalles del nodo.

## Criterios de Aceptación

1. El sistema debe mostrar los siguientes datos básicos del nodo:
   - Nombre
   - Latitud
   - Longitud

   ademas de los datos anteriores debe mostrarse los siguientes datos de los inversores:
   - Nombre
   - NumeroSerie

   ademas de los datos anteriores debe mostrarse los siguientes datos de los sensores:
   - Nombre
   - NumeroSerie
   - Endpoint (si tiene, sino mostrar que no tiene endpoint asociado)
2. Se debe poder acceder al detalle del nodo a travez de clickear en el listado de nodos, dentro de la pagina de detalles del parque solar, se debe poder clickear en cualquier parte del listado del nodo para poder acceder al detalle del nodo.
3. Debe poderse volver al parque solar del nodo con un boton "Volver al parque".
4. Debe tener un boton "+ Agregar Panel Solar" que de momento NO tendra utilidad, hasta un requerimiento futuro.
5. Seguridad: Solo los usuarios autenticados con el rol de "Administrador" están autorizados a realizar esta acción.
