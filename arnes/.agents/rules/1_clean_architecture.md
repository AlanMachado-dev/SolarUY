# Reglas de Arquitectura y Diseño (Proyecto Blazor Clean Architecture)

Este proyecto sigue los principios de Clean Architecture. Al generar, refactorizar o validar código, el agente debe asegurar que se cumplan las siguientes reglas (preparadas para ser validadas en el futuro con ArchUnitNET):

## 1. Reglas de Dependencia de Capas (El Círculo de Clean Architecture)
- **Aislamiento del Dominio:** La capa de `Domain` es el núcleo. **NUNCA** debe importar paquetes de Entity Framework Core, ASP.NET, componentes de UI, ni hacer referencias a otras capas del proyecto. Solo usa C# puro.
- **Aislamiento de la Aplicación:** La capa de `Application` (Casos de uso / Servicios) solo puede depender de `Domain`. No puede conocer nada de Blazor ni detalles de acceso a datos (como DbContext o SQL).
- **Protección de la Presentación:** La capa de UI (Componentes de Blazor) **NO DEBE** acceder directamente a la capa de `Infrastructure` (ej. repositorios concretos o DbContext). Blazor solo se comunica con la capa de `Application` (Servicios).
- **La Infraestructura es un detalle:** La capa de `Infrastructure` (Base de datos, APIs) implementa las interfaces definidas en `Application`. Ninguna otra capa debe depender de `Infrastructure` directamente (la inyección se configura solo al arrancar la app).

## 2. Reglas de Dominio y Datos
- **Flexibilidad en Entidades:** Se permite el uso de `setters` públicos (`public set`) en las entidades del Dominio para facilitar el aprendizaje con Entity Framework Core. Sin embargo, si una actualización de datos requiere validaciones complejas, el agente debe proponer encapsularla en un método de la entidad en lugar de hacer la lógica dispersa.
- **Uso de Clases Anémicas Permitido Inicialmente:** Está permitido que las entidades funcionen principalmente como contenedores de datos en etapas tempranas.

## 3. Reglas de Repositorios y Abstracciones
- **Inversión de Dependencias (DIP):** Todo acceso a la base de datos se hace mediante interfaces. Las interfaces de los repositorios (ej. `IUsuarioRepository`) se definen idealmente en `Application`, pero su implementación concreta (ej. `UsuarioRepository`) va **obligatoriamente** en `Infrastructure`.
- **Convención de Nombres:** Las clases que implementan acceso a base de datos deben terminar con el sufijo `Repository`. Las interfaces deben comenzar siempre con la letra `I`.

## 4. Inyección de Dependencias
- **Inyección por Constructor:** Las dependencias en los Servicios de Aplicación y Repositorios deben inyectarse EXCLUSIVAMENTE a través del constructor. No instanciar clases complejas con `new` si corresponden a servicios, repositorios o componentes de infraestructura.
