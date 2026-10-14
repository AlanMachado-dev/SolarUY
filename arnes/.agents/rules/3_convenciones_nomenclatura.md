# Convenciones de Nomenclatura en .NET (Clean Architecture)

Al momento de generar código, refactorizar o proponer soluciones, el agente debe respetar de manera estricta las convenciones de nomenclatura estándar de C# y del proyecto, con el objetivo de mantener un código legible, uniforme y predecible. Estas convenciones son evaluadas mediante pruebas automatizadas con ArchUnitNET.

## 1. Reglas Generales de C#
- **Interfaces:** Toda interfaz debe comenzar invariablemente con la letra `I` mayúscula (ej. `IUsuarioRepository`, `ICorreoService`).
- **Excepciones:** Cualquier clase personalizada que herede de `System.Exception` debe terminar con el sufijo `Exception` (ej. `SaldoInsuficienteException`).
- **PascalCase vs camelCase:**
  - `PascalCase`: Se utiliza para nombres de Clases, Records, Interfaces, Métodos y Propiedades públicas.
  - `camelCase`: Se utiliza para variables locales y parámetros de métodos.
  - `_camelCase` (con guion bajo): Se utiliza EXCLUSIVAMENTE para variables de instancia privadas (fields), en especial aquellas inyectadas por dependencia en el constructor (ej. `private readonly IUsuarioRepository _usuarioRepository;`).

## 2. Reglas de Capas (Clean Architecture)
- **Repositorios (Infrastructure):** Toda clase que gestione el acceso a datos debe tener el sufijo `Repository`. Nunca debe usarse "Dao" o "GestorDatos". Su abstracción correspondiente debe llamarse igual, prefijada con I (ej. `IClienteRepository` -> `ClienteRepository`).
- **Servicios (Application):** Las clases que coordinan la lógica de negocio o actúan como Casos de Uso deben terminar con el sufijo `Service` (ej. `FacturacionService`).
- **Controladores / Componentes de UI (Presentation):** 
  - Si hay una API REST, los controladores deben terminar en `Controller`.
  - Para los componentes Blazor (archivos `.razor`), deben tener nombres descriptivos en PascalCase sin sufijos técnicos a menos que sean páginas completas (pueden llevar el sufijo `Page`).

## 3. Acrónimos
Si se utilizan acrónimos de dos letras (ej. ID, IP), ambas deben ir en mayúscula si es la primera palabra (ej. `IDUsuario`) o PascalCase si están en medio (ej. `UsuarioId`). Los acrónimos de más de dos letras deben tratarse de la misma forma (ej. `XMLDocument`).

## 4. Idioma (Spanglish Técnico)
- **Dominio en Español:** Los nombres base de las entidades, variables, propiedades y métodos que representan reglas de negocio deben escribirse en **Español** (ej. `Factura`, `CalcularTotal()`, `_clienteActivo`).
- **Sufijos Estructurales en Inglés:** Debes respetar obligatoriamente los sufijos y prefijos técnicos en **Inglés** exigidos en las secciones anteriores. **No debes traducirlos**. (Ejemplo correcto: `FacturaRepository`, `UsuarioService`, `PagoRechazadoException`).
