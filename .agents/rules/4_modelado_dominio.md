# Reglas Avanzadas: Modelado de Dominio

Estas reglas están diseñadas para garantizar que el diseño de software sea verdaderamente orientado a objetos.

## Diseño Orientado a Objetos vs. Orientado a Base de Datos
- **Directiva:** Las clases en la capa de `Domain` deben modelarse pensando en objetos, comportamiento y lenguaje ubicuo, **NO** como un mero reflejo de tablas SQL para facilitar el trabajo del ORM (ej. Entity Framework).
- **Relaciones Ricas:** Fomenta activamente el uso de propiedades de navegación ricas (referencias a objetos reales, ej: `public Cliente Cliente { get; private set; }`) y evita la "obsesión por los tipos primitivos", es decir, exponer ciegamente claves foráneas crudas (ej: `public int ClienteId { get; set; }`) sin encapsular la relación semántica de los objetos.
- **Comportamiento sobre Datos:** Si detectas que un usuario modela una entidad (ej. `Factura`) y la lógica que muta su estado (ej. cambiar de estado a 'Pagada', calcular totales) se realiza en un servicio externo dejando a la entidad solo con `get/set`, debes advertirlo. Sugiere mover esa lógica **adentro** de la propia entidad (Rich Domain Model), para evitar el anti-patrón de Dominio Anémico (Anemic Domain Model).
