# Tareas — req011

## Domain

- [x] Implementar las transiciones del panel y comprobar su compatibilidad con las lecturas.
- [x] Probar idempotencia, estados protegidos y lecturas contradictorias.

## Application

- [x] Agregar el servicio autorizado de control y extender el contrato de monitoreo.

## Infrastructure

- [x] Persistir los cambios y las lecturas con consistencia transaccional por panel.

## Presentation

- [x] Integrar las acciones en el panel de control y hacer que el inversor simulado siga el estado persistido.

## Verificación

- [x] Compilar la solución y ejecutar Domain y ArchUnitNET. Pasaron 112 y 17 pruebas, respectivamente.
- [x] Verificar en MySQL aislado los cambios, la persistencia, la idempotencia y las lecturas contradictorias. La prueba también comprobó la reversión ante un fallo de escritura.
- [ ] Confirmar en el navegador con Auth0 la visibilidad para los roles y la actualización en vivo.

Para la prueba en navegador, abrir el monitoreo de un panel como Administrador, desactivarlo y verificar 0 W. Recargar para confirmar que el estado se conserva, activarlo y esperar una nueva lectura del simulador. En una sesión de Operador, comprobar que el estado es visible y que el botón no aparece.
