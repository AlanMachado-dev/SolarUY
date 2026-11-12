# req010 - Especificaciones técnicas de generación de energía

Fuente de alcance [requirements.md](requirements.md). La fuente es `Medicion.GeneracionActualW`, con su fecha UTC y panel asociado. La estimación se obtiene al consultar el detalle.

## Domain

- Por cada panel, considerar pares consecutivos con fechas distintas, separados por un máximo de 40 segundos. Ese límite equivale a dos intervalos nominales del simulador. En un empate de fecha conservar la lectura con el mayor ID según el orden estable del repositorio.
- Para cada par válido, tomar la potencia de la primera lectura como constante hasta la segunda. Usar solo los segundos del tramo que caen entre la medianoche local y el corte de consulta. Calcular `potencia W × segundos / 3 600 000` y sumar con precisión decimal. Una lectura de 360 W seguida de otra a los 20 segundos aporta 0,002 kWh.
- Devolver la energía y la cobertura por panel. Un tramo válido de potencia cero aporta 0 kWh. Sin tramos válidos, la energía es indeterminada. Marcar como parcial la cobertura que empieza después de medianoche, tiene huecos internos o termina más de 40 segundos antes del corte. Los últimos 40 segundos pueden estar pendientes de la siguiente lectura y quedan fuera de la energía estimada.

## Application

- Crear un servicio de consulta de energía. Autorizar a Administrador y Operador antes de acceder a las mediciones. Recibir el tipo de componente y su ID, y usar `TimeProvider` una vez por consulta para obtener el corte UTC y la medianoche del día en `America/Montevideo`.
- Definir `IEnergiaRepository` para obtener los paneles esperados del componente y sus lecturas en lote. Invocar el cálculo de Domain por panel y sumar los resultados disponibles con precisión decimal. El agregado es parcial si algún panel tiene cobertura parcial o carece de datos suficientes y al menos otro aporta datos.
- Entregar un DTO con el valor opcional en kWh, el estado de cobertura, el inicio del período, el corte y los conteos de paneles esperados y con tramos válidos. Distinguir «Sin paneles» de «Sin datos suficientes».

## Infrastructure

- Implementar `EnergiaRepository` con EF Core y el mapeo existente de `Medicion`. Resolver el alcance por panel, por su nodo o por el parque de su nodo. Proyectar por separado los IDs de los paneles esperados y las lecturas, con consultas de solo lectura en lote. Así se incluyen los paneles sin muestras en el cálculo de cobertura.
- Traer las lecturas desde 40 segundos antes de la medianoche local convertida a UTC hasta el corte inclusive, ordenadas por panel, fecha UTC e ID. El margen anterior permite calcular la parte de un tramo que cruza la medianoche. Reutilizar el índice existente por panel y fecha.
- Registrar el repositorio y el servicio en la composición de la aplicación. La estimación se deriva de las tablas existentes.

## Presentation

- Agregar el indicador en `DetalleParqueSolar.razor`, `DetalleNodo.razor` y `MonitoreoPanel.razor`. Consultarlo desde servidor para el componente de la ruta cuando el detalle esté disponible para el usuario.
- Mostrar «Energía estimada de hoy» en kWh, con la hora de corte en Uruguay y el mensaje de cobertura que corresponda. Aplicar formato de pantalla después de sumar y representar los valores positivos pequeños de forma que se distingan de cero.
- En el parque, mostrar el indicador a Administrador y Operador y conservar el acceso al resto del detalle. En el panel, cargar la estimación tanto para Operador como para Administrador, junto al medidor en vivo. La vista presenta el DTO que devuelve Application.
