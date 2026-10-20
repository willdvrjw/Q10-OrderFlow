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
**Costo:** no resuelve sincronización de catálogo mutable. Se documenta como evolución a eventos de producto en producción.

## ADR-010: Docker Compose primero, Kubernetes también planificado
**Decisión:** Compose es fuente de ejecución obligatoria; manifiestos Kubernetes para Orders se modelan desde el inicio.
**Por qué:** test reproducible y bonus infra sin duplicar reglas de negocio.
**Costo:** configuración adicional; no se despliega un clúster innecesario.

## Reglas para cambios

Toda decisión que cambie debe actualizar este documento, la guía de defensa y el plan de pruebas. El cambio deberá identificarse por ruta exacta de archivo, justificación, consecuencias y pruebas de regresión.
