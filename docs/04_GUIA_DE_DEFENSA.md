# 04. Guía de defensa técnica — Q10-OrderFlow

Esta guía describe **el código efectivamente incorporado en el repositorio**, no rutas hipotéticas. Su objetivo es que el candidato pueda explicar, localizar, modificar y comprobar el sistema durante una entrevista en vivo. No memorizar respuestas sin entender su implementación.

## Recorrido de negocio en 90 segundos

1. El usuario registra nombre, SKU existente y cantidad de 1 a 100 desde React. La API valida nuevamente, porque el frontend no es una frontera confiable.
2. `POST /orders` responde HTTP **201 Created** con `Pending`. El pedido y `OrderCreated` se guardan en la misma transacción PostgreSQL; el evento queda en Outbox.
3. `OrdersOutboxDispatcher` publica `OrderCreated` a RabbitMQ con confirmación. Si el broker falla, el pedido no se pierde: el evento queda pendiente.
4. `OrderCreatedConsumer` consume el evento con ACK manual. `OrderCreatedHandler` registra el `OrderId` único, ejecuta el descuento atómico condicionado al stock y registra el resultado y su Outbox en una transacción.
5. `InventoryOutboxDispatcher` publica `StockReserved` o `StockRejected`. `StockResultHandler` registra el `EventId` en Inbox y transiciona el pedido terminalmente a `Confirmed` o `Rejected`.
6. SignalR emite `ordersChanged` para pedir al navegador actualizar datos; un polling periódico a `GET /orders` recupera la información si WebSocket se interrumpe.

**Invariantes:** no hay confirmación sin reserva; no se descuenta dos veces el mismo pedido; no hay stock negativo; los estados terminales no revierten; cada servicio es dueño de su base.

## Preguntas de entrevista y criterios

**¿Por qué Pending en el POST?** La reserva ocurre en otro proceso y no debe asumirse exitosa por el simple registro del pedido. HTTP 201 confirma creación del recurso, no inventario.

**¿Qué diferencia 400 de Rejected?** `400` rechaza una entrada inválida (cliente vacío, SKU inexistente, cantidad fuera de rango). `Rejected` es el estado de un pedido válido que no obtuvo inventario; su creación respondió 201.

**¿Por qué dos bases?** Orders e Inventory tienen propiedad independiente de sus datos. Comparten una instancia PostgreSQL por simplicidad de la demostración, pero no comparten tablas ni transacciones.

**¿Qué significa consistencia eventual?** Una consulta inmediatamente posterior al POST puede ver `Pending`, y más tarde `Confirmed` o `Rejected` cuando lleguen los eventos.

**¿Para qué Outbox?** PostgreSQL y RabbitMQ no participan en un mismo COMMIT atómico. Persistir evento y cambio de negocio juntos evita el hueco entre guardar el pedido y publicar el evento.

**¿Exactly-once?** No prometemos entrega única por red. RabbitMQ puede reentregar mensajes. El efecto de negocio está protegido por una clave única `OrderId` y la transacción de inventario; `EventId` identifica cada entrega o resultado.

**¿Y si dos reservas llegan simultáneamente?** PostgreSQL ejecuta un UPDATE condicionado a `disponible >= cantidad`, de modo que las operaciones concurrentes no pueden consumir más unidades que las disponibles. Se probó con dos DbContext y transacciones separadas.

**¿Cómo se crean nuevos SKU sin romper la independencia?** Orders valida unicidad, solicita a Inventory crear existencias iniciales de modo idempotente y después persiste el nombre en su catálogo. Si falla entre los dos pasos, una repetición idéntica puede completar el alta sin restablecer stock. Este HTTP interno solo es administrativo; **los pedidos continúan usando RabbitMQ**. No se promete transacción distribuida.

**¿Qué sucede si RabbitMQ está caído?** Orders sigue admitiendo pedidos y almacenando eventos. Al regresar el broker, el despachador reintenta y el pedido termina de procesarse. Esto se observó en pruebas reales.

**¿Qué sucede si el Worker está detenido?** El pedido queda en `Pending`, el broker conserva el evento y el procesamiento continúa al reanudar el Worker. También se verificó.

**¿Qué pasa entre COMMIT y ACK?** Ante fallo, RabbitMQ puede entregar otra vez el evento. El consumidor debe reconocer que `OrderId` ya fue procesado; no repetir stock. La idempotencia se probó en PostgreSQL, pero no se forzó una caída exactamente en ese instante.

**¿Qué hace la DLQ?** Mensajes irrecuperables (formato o versión inválidos) se apartan para diagnóstico en lugar de provocar reintentos infinitos.

**¿Por qué SignalR y polling?** SignalR permite respuesta inmediata; polling recupera coherencia si falla WebSocket. Se verificó en el navegador un frame `ordersChanged` y, en una prueba aparte, polling con la ruta SignalR bloqueada.

**¿Por qué un solo pod en Kubernetes?** Los publicadores Outbox aún no incorporan leasing distribuido y SignalR usa conexiones locales. Una sola réplica evita suposiciones de escalamiento no probadas. El bonus trae recursos declarativos y no exige clúster activo.

**¿Por qué Inventory expone una API HTTP si ya consume RabbitMQ?** El pedido sigue usando eventos asincrónicos. La consulta y la entrada manual de existencias son operaciones administrativas dirigidas al dueño del stock; pasar por Orders violaría la independencia de bases. Nginx publica `/inventory` hacia el puerto HTTP interno del Worker.

**¿Qué ocurre si alguien modifica inventario mientras preparo un ajuste?** El formulario envía `existenciaEsperada`. Inventory ejecuta un único `UPDATE ... WHERE disponible = existenciaEsperada` y, si cambió el stock, devuelve 409. Así un ajuste con datos antiguos no pisa reservas recientes.

**¿Una reposición confirma automáticamente pedidos rechazados?** No. `Rejected` es terminal, igual que `Confirmed`. La reposición modifica exclusivamente las existencias disponibles; una reserva nueva —o un `Pending` todavía sin procesar— utiliza el stock que exista cuando Inventory ejecute la reserva. «Crear nuevo pedido» copia los datos de un rechazado a un formulario, pero nunca reabre ni envía automáticamente el anterior.

**¿Por qué no puedo cambiar la cantidad de un rechazado al repetirlo?** El flujo de repetición está destinado a reintentar la misma solicitud, no a crear un pedido distinto encubierto. Se conserva exactamente cliente, SKU y cantidad original. Antes del POST se consulta el rechazo real por `GET /orders/{id}` y el stock actual; los cambios de cantidad se indican al instante y bloquean el envío. Para una cantidad distinta, se cancela la repetición y se utiliza el registro normal. El backend sigue aceptando pedidos válidos como `Pending` para mantener las reglas de Q10.

**¿Cómo se actualiza el stock en vivo?** Inventory emite `inventoryChanged` por su propio canal SignalR después del COMMIT de cada ajuste o descuento; el navegador recupera `GET /inventory`. Si falla WebSocket, sigue consultando periódicamente. La actualización de los estados de pedidos conserva su canal independiente `ordersChanged` y sus mismas transiciones.

## Rutas exactas de la implementación y cómo modificarla

| Qué te pueden pedir | Archivo(s) real(es) | Comprobación |
|---|---|---|
| Cambiar validación del pedido | `src/Orders.Api/Application/Orders/OrderValidator.cs`, `src/Orders.Api/Domain/Order.cs` | `OrderBusinessRulesTests.cs`, HTTP 400 |
| Cambiar límite de cantidad en el formulario | `web/src/components/OrderForm.tsx` **y** validaciones backend mencionadas arriba | Formulario + HTTP + tests |
| Cambiar POST, GET o respuesta | `src/Orders.Api/Endpoints/OrdersEndpoints.cs` | curl y tabla del frontend |
| Cambiar estados o transiciones | `src/Orders.Api/Domain/OrderStatus.cs`, `src/Orders.Api/Domain/Order.cs` | Tests de dominio y flujo |
| Cambiar catálogo o cantidades iniciales | `seed/products.json`; carga en `src/Orders.Api/Application/Orders/ProductCatalog.cs` y `src/Inventory.Worker/Infrastructure/Persistence/InventoryDatabaseInitializer.cs` | Arranque con volumen nuevo; no sobrescribir stock consumido |
| Registrar nuevos productos dinámicos y validar SKU único | `src/Orders.Api/Endpoints/ProductsEndpoints.cs`, `src/Orders.Api/Application/Orders/InventoryProductProvisioner.cs`, `src/Inventory.Worker/Application/InventoryProductRegistrationService.cs`, `web/src/components/NewProductForm.tsx` | POST /products: HTTP 201 o 409; verificar GET /products, GET /inventory, reinicio y prueba de idempotencia PostgreSQL |
| Cambiar contratos de eventos | `src/OrderFlow.Contracts/Events/OrderEvents.cs` | Compilar productores y consumidores; verificar serialización |
| Cambiar rutas o colas RabbitMQ | `src/OrderFlow.Messaging/RabbitMqTopology.cs` | Consumo/publicación y DLQ |
| Cambiar confirmación de publicación | `src/OrderFlow.Messaging/RabbitMqTransport.cs` | Broker disponible / indisponible |
| Cambiar reintentos o ACK del consumidor | `src/OrderFlow.Messaging/RabbitMqConsumerLoop.cs` | Recuperación Worker/broker |
| Cambiar publicación de pedidos | `src/Orders.Api/Infrastructure/Messaging/OrdersOutboxDispatcher.cs` | Detener broker, crear, recuperar |
| Cambiar recepción y aplicación de resultados | `src/Orders.Api/Infrastructure/Messaging/StockResultConsumer.cs`, `src/Orders.Api/Application/Orders/StockResultHandler.cs` | Estado terminal e Inbox |
| Cambiar modelo/tablas de pedidos | `src/Orders.Api/Infrastructure/Persistence/OrdersDbContext.cs` | Probar sobre base nueva; documentar esquema |
| Cambiar la notificación en vivo de existencias | `src/Inventory.Worker/Infrastructure/Messaging/SignalRInventoryChangeNotifier.cs`, `web/src/components/InventoryManager.tsx` | SignalR `/hubs/inventory`, polling de respaldo |
| Crear nueva solicitud desde un pedido rechazado | `web/src/components/OrdersList.tsx`, `web/src/components/OrderForm.tsx`, `web/src/App.tsx` | Precarga sin alterar pedido original |
| Consultar o editar stock desde la UI | `web/src/components/InventoryManager.tsx`, `web/src/lib/api.ts` | GET /inventory, POST /inventory/adjustments |
| Cambiar la validación del ajuste administrativo | `src/Inventory.Worker/Endpoints/InventoryEndpoints.cs` | HTTP 400, 404 y 409 |
| Cambiar ingreso, reemplazo o bloqueo optimista de stock | `src/Inventory.Worker/Application/InventoryManagementService.cs` | Prueba PostgreSQL de conflicto tras reserva y ajustes |
| Cambiar lógica de reserva, idempotencia o SQL atómico | `src/Inventory.Worker/Application/OrderCreatedHandler.cs` | `InventoryIntegrationTests.cs` en PostgreSQL |
| Cambiar las tablas de stock, pedidos procesados u Outbox de Inventory | `src/Inventory.Worker/Infrastructure/Persistence/InventoryDbContext.cs` | Tests PostgreSQL aislados |
| Cambiar publicación del resultado de inventario | `src/Inventory.Worker/Infrastructure/Messaging/InventoryOutboxDispatcher.cs` | Evento reservado/rechazado |
| Cambiar datos mostrados, polling o SignalR | `web/src/hooks/useOrdersLive.ts`, `web/src/types.ts`, `web/src/lib/api.ts` | WebSocket 101, frame de evento y fallback |
| Cambiar formulario | `web/src/components/OrderForm.tsx` | Errores visibles y POST |
| Cambiar tabla/filtros/estados visibles | `web/src/components/OrdersList.tsx` | UI con pedidos Pending/Confirmed/Rejected |
| Cambiar estilos responsivos | `web/src/style.css` | Vista escritorio y móvil |
| Cambiar puertos, proxy y variables del despliegue | `compose.yaml`, `infra/nginx/default.conf` | `docker compose config --quiet` y GET /health/ready |
| Cambiar probes/límites Kubernetes | `k8s/orders-api-deployment.yaml` | `kubectl kustomize k8s` y revisar YAML resultante |

Las rutas de esta tabla corresponden a archivos existentes en el repositorio. No editar solo el frontend cuando la regla de negocio debe permanecer en el servidor.

## Evidencia disponible y límites de las pruebas

- **Aprobado:** cinco tests backend, incluidos idempotencia y concurrencia con PostgreSQL; estado Pending/Confirmed/Rejected; validaciones HTTP; recuperaciones de Worker y broker; SignalR, polling y persistencia tras reinicio.
- **Aprobado en el mismo Codespace:** clon local con redes y volúmenes nuevos, endpoints HTTP operativos y pedido confirmado. El Codespace requirió una corrección temporal de `iptables-legacy` específica del host.
- **Kubernetes:** manifiestos ensamblados con `kubectl kustomize k8s`; no se ha desplegado clúster.
- **No afirmado:** ejecución independiente en un host Docker distinto ni fallo inyectado exactamente entre COMMIT y ACK.

La evidencia detallada está en `docs/09_EVIDENCIAS_VALIDACION.md`.

## Qué cambiaría para producción

Migraciones versionadas; roles mínimos y secretos gestionados; catálogo de propiedad única; reintentos con política y observabilidad completa; coordinación de Outbox para réplicas; SignalR con soporte multinodo; alertas, backups y más pruebas de caos.

La prioridad en esta evaluación es mantener funcionamiento comprobable y poder explicar exactamente cada compromiso técnico.
