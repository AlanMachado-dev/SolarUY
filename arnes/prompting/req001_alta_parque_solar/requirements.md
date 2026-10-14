# Requerimientos: Alta de Parque Solar

## Descripción General
Como Administrador del sistema SolarUY, quiero poder registrar un nuevo Parque Solar en la plataforma para poder gestionar de forma independiente sus nodos, paneles e inversores.

## Criterios de Aceptación
1. El sistema debe permitir el ingreso de los siguientes datos básicos del parque:
   - ID (Obligatorio, único en el sistema, autogenerado).
   - Nombre (Obligatorio).

   - Ubicación Localidad / Dirección (Obligatorio).
   - Capacidad Total Estimada en MW (Calculado mediante la suma de la generación máxima de los paneles que son partes de los nodos del parque). (Cuando se crea el parque por defecto queda en 0, hasta que se añadan nodos y paneles en los mismos)
2. Seguridad: Solo los usuarios autenticados con el rol de "Administrador" están autorizados a realizar esta acción.
3. Estado inicial: Al crear el parque, este se registra sin nodos ni paneles asignados todavía.

## Reglas de Negocio
- No pueden existir dos parques solares con el mismo nombre.
- El alta de un parque es el paso previo y obligatorio antes de poder configurar la topología del mismo (nodos, inversores y paneles).
