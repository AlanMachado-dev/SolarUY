# Requerimientos: Implementación OpenID

## Descripción General
Integrar al proyecto un sistema de autenticación/autorización externo que cumpla con el estándar OpenID.

## Criterios de Aceptación
1. El sistema debe permitir iniciar sesión usando un sistema externo que cumpla con el estándar OpenID. Se recomienda Auth0. Debe permitir diferenciar entre roles Administrador y Operador para usarse a través del sistema.
2. Estado inicial: En un principio, existirá un usuario ya creado de rol Administrador.

## Reglas de Negocio
 - Adaptar la implementación actual para que funcione correctamente con el nuevo sistema externo.
 - Crear documentación breve sobre cómo funciona y cómo usarlo
