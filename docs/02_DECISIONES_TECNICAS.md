# 02. Registro de decisiones arquitectónicas (ADR)

Estado inicial: propuestas adoptadas para implementación, sujetas a corrección si una prueba objetiva detecta problemas.

## ADR-001: C# / .NET 10 LTS
**Decisión:** ASP.NET Core 10 y Worker Service 10.
**Por qué:** cumple .NET 8+, coincide con stack señalado por Q10, soporte hasta 2028 y bibliotecas maduras.
**Costo:** aprendizaje y disponibilidad del SDK/herramientas. Docker permitirá compilar sin SDK local.

## ADR-002: PostgreSQL con datos segregados por servicio
**Decisión:** una instancia contenedorizada y dos bases, una para pedidos y otra para inventario.
**Por qué:** límites claros, transacciones locales y ninguna lectura directa de la base ajena.
**Costo:** más inicialización. Se resuelve en los scripts automáticos del contenedor y las migraciones.

## ADR-003: Outbox transaccional en cada productor
**Decisión:** almacenar negocio y eventos en una transacción; publicar desde worker de despacho, tras confirmar RabbitMQ.
**Por qué:** impide el hueco entre guardar el pedido y publicar el evento o entre descontar stock y notificarlo.
**Costo:** tablas y trabajo periódico de despachadores. Proporcional al requisito explícito de fallos del broker.

## ADR-004: Inbox + unicidad por orderId
**Decisión:** eventId detecta entregas repetidas; orderId impide repetir el efecto de reserva.
**Por qué:** distinguir «evento» de «operación de negocio» y mantener idempotencia real.
**Costo:** almacenamiento mínimo de procesamiento de eventos.

## ADR-005: SQL de descuento condicional
**Decisión:** una sentencia de actualización que solo afecte filas con unidades suficientes, dentro de transacción.
**Por qué:** previene sobreventa cuando dos peticiones concurren sobre el mismo SKU.
**Costo:** operación específica de PostgreSQL encapsulada en un repositorio, para reemplazarla si cambia el proveedor.

## ADR-006: RabbitMQ.Client oficial en vez de framework integral
**Decisión:** capa pequeña de transporte con cliente RabbitMQ 7.x, ACK manual y publisher confirms.
**Por qué:** mostrar dominio de fiabilidad y hacer visible el criterio técnico sin esconder toda la lógica en un framework de orquestación.
**Costo:** debemos escribir y probar cuidadosamente conexión, reconexión, publicación y consumo.

## ADR-007: SignalR con recuperación por polling
**Decisión:** SignalR como mejora visible y consulta periódica como respaldo.
**Por qué:** cumple el bonus y conserva confiabilidad de la vista ante reconexiones.
**Costo:** mantener un canal en vivo y su recuperación. Un solo hook abstraerá esa complejidad.

## ADR-008: React + TypeScript + CSS modular
**Decisión:** panel con un pequeño número de componentes independientes y diseño avanzado pero ligero.
**Por qué:** afinidad con vacantes públicas de Q10 y menor sobrecarga de librerías.
**Costo:** estilos propios y atención a accesibilidad.

## ADR-009: Catálogo estático de prueba con una semilla compartida
**Decisión:** el catálogo de SKU inicial se entrega como dato versionado para validación local de Orders y stock inicial de Inventory.
**Por qué:** el enunciado no requiere altas de productos y prohíbe obligar a pasos manuales.
**Costo histórico:** esta decisión se conserva para la semilla inicial y los tres SKU de ejemplo. La ampliación de catálogo mutable se detalla en ADR-011.

## ADR-010: Docker Compose primero, Kubernetes también planificado
**Decisión:** Compose es fuente de ejecución obligatoria; manifiestos Kubernetes para Orders se modelan desde el inicio.
**Por qué:** test reproducible y bonus infra sin duplicar reglas de negocio.
**Costo:** configuración adicional; no se despliega un clúster innecesario.

## ADR-011: Alta dinámica de productos sin alterar el ciclo de pedidos
**Decisión:** conservar la semilla versionada, guardar altas posteriores en `catalog_products` (base de Orders) y crear existencias iniciales en `inventory_product_registrations` + `stock_items` (base de Inventory).

**Por qué:** el usuario necesita poder dar de alta productos desde el panel, que aparezcan en pedidos y existencias y sobrevivan a reinicios. Orders sigue validando únicamente SKU conocidos y jamás modifica la tabla de inventario. Los eventos `OrderCreated` y `StockReserved/Rejected` y los estados terminales permanecen sin cambios.

**Coordinación:** `POST /products` valida nombre/SKU/cantidad, solicita el aprovisionamiento a Inventory mediante HTTP interno y solo después guarda el SKU en Orders. Inventory escribe su registro idempotente y el stock en una transacción PostgreSQL. Si falla la segunda escritura, repetir **exactamente el mismo SKU/nombre/stock inicial** completa la operación sin resetear existencias. Los SKU duplicados con otros datos y los existentes en la semilla se rechazan.

**Esquemas previos:** se incorporan tablas mediante `CREATE TABLE IF NOT EXISTS` tras `EnsureCreated`. Esta extensión es intencionalmente no destructiva con volúmenes ya poblados; en producción corresponde gestionar migraciones versionadas.

**Compromiso:** no existe un commit distribuido entre Orders e Inventory. Un fallo entre ambos pasos puede dejar un registro provisional de Inventory; reintentar la misma petición lo recupera. Es preferible a anunciar como vendible un SKU que Inventory desconoce. No confundir este alta coordinada con la reserva de pedidos: **los pedidos siguen siendo asíncronos y resistentes a caída de RabbitMQ**.

**Frontera de seguridad:** las rutas administrativas son abiertas solo a efectos de demo local; autenticación/roles y auditoría serían obligatorios antes de exponerlas públicamente. El bonus Kubernetes actual no prueba aún el aprovisionamiento de productos.

**Validación observada:** los dos ensayos de integración PostgreSQL de alta dinámica pasaron junto con las cinco pruebas originales (**7/7**) en Codespaces. El usuario confirmó el alta desde la interfaz, creación de pedido y cambio de stock; verificó HTTP 409 en SKU duplicado y conservación byte a byte de catálogo, inventario e historial al reiniciar los servicios. Las pruebas en otros sistemas operativos y una inyección exacta de caída entre COMMIT y ACK siguen pendientes.

## Reglas para cambios

Toda decisión que cambie debe actualizar este documento, la guía de defensa y el plan de pruebas. El cambio deberá identificarse por ruta exacta de archivo, justificación, consecuencias y pruebas de regresión.
