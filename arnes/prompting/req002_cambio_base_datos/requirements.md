# Requerimientos: Cambiar Base de Datos

## Descripción General
Adaptar el sistema existente para que deje de usar SQLite y pase a usar como motor de base de datos MySQL.

## Criterios de Aceptación
1. El sistema debe poder buscar y escribir datos a la base de datos correctamente, usando MySQL en lugar de la implementación actual de SQLite. Se deberá poder configurar los datos de conexión para poder conectarse a la base de datos que esté corriendo en el sistema.
2. Ordenado: Deberá quedar bien organizado la estructura que se genere para la conexión de la base de datos.
3. Limpieza: No debe quedar rastro de la implementación anterior. Esto implica cambiar o eliminar las clases existentes si es necesario para que funcione con MySQL. Generar un docuemnto de como hacer la conexión con la base de datos del sistema.

## Reglas de Negocio
- No modificar nada en la carpeta de prompting/req001_alta_parque_solar
