# Reglas Avanzadas: Capa de Presentación

Estas reglas están diseñadas para garantizar que se respeten las fronteras de responsabilidad semántica de la interfaz de usuario.

## Prohibición de Lógica de Negocio en la Capa de Presentación (Blazor)
- **Directiva:** Los componentes de la Interfaz de Usuario (archivos `.razor` o su *code-behind*) deben ser estrictamente "Vistas Tontas" (Dumb Views). Su única responsabilidad es capturar eventos del usuario, invocar casos de uso y mostrar datos.
- **Validación Semántica:** Como agente de IA, **DEBES** rechazar o advertir firmemente si el usuario solicita o implementa lógica condicional de negocio (ej. usar un `if / switch` para determinar reglas críticas del dominio) o cálculos matemáticos (ej. iterar un `foreach` para sumar los impuestos de un carrito de compras) directamente dentro de un componente de UI.
- **Corrección Exigida:** Toda lógica de cálculo o decisión debe ocurrir en el Dominio o en un Servicio de Aplicación. Blazor solo debe invocar un método en la capa de `Application` o recibir un DTO (Data Transfer Object) con los datos ya calculados y listos para ser renderizados.
