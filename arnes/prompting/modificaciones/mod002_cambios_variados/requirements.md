# mod002 — Modificaciones: Cambios Variados

> Identificador vigente: **mod002 (Cambios Variados)**.

## Descripción General

Adaptar el sistema existente con mejoras en varios aspectos, con el objetivo de corregir detalles no contemplados en los requerimientos.

## Criterios de Aceptación

1. Al crear parque se debe redirigir al detalle del parque, no una pantalla que muestra los detalles y no tiene para agregar el nodo. Modificar el AltaParqueSolar.razor para redirigir al detalle si el creado del parque fue satistactorio, mostrando una alerta en el detalle que le indique al usuario que la creacion del parque fue exitosa.
2. En el detalle de parque solar mostrar la lista de nodos con sus datos, asi queda claro que fue creado correctamente.
3. En inversor hacer que el NumeroSerie sea unico, que no se pueda repetir en todo el sistema.
4. En sensor hacer que el NumeroSerie sea unico, que no se pueda repetir en todo el sistema.

## Reglas de Negocio

- No modificar nada en la carpeta de arnes/prompting/requerimientos ni carpetas dentro de la misma.
