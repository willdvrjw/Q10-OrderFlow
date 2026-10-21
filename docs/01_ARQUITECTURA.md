# 01. Arquitectura técnica — Q10-OrderFlow

## Estado y principios

Documento de diseño previo a implementación. Especifica responsabilidades verificables, no declara código ejecutado. Invariantes: nunca confirmar sin reserva, nunca reservar dos veces para el mismo pedido, nunca permitir stock negativo, nunca perder un pedido confirmado por la API cuando falla el broker, no acoplar los servicios mediante acceso cruzado a sus datos.

## Módulos y límites

**Frontend (React + TS):** formulario, validación de experiencia de usuario, tabla de pedidos, estados visibles, notificaciones y actualización SignalR con fallback de polling. La API vuelve a validar todo: el cliente no es fuente de verdad.

**Orders API (ASP.NET Core):** REST `POST /orders`, `GET /orders`, `GET /orders/{id}`; validación de catálogo; almacenamiento de pedido `Pending`; registro transaccional del evento `OrderCreated` en su outbox; consumo de resultados de stock; transición monotónica a `Confirmed` o `Rejected`; SignalR; endpoints `/health/live` y `/health/ready`.

**Inventory Worker (.NET):** expone una API HTTP interna de inventario para la consola administrativa de demostración (GET y ajustes de existencias), sin dar a Orders acceso a stock; escucha `OrderCreated`; verifica si el pedido ya tiene una reserva registrada; ejecuta descuento atómico condicionado al stock; almacena el resultado de ese pedido y el evento saliente en una sola transacción; reconoce mensajes solamente tras confirmar la transacción.

**RabbitMQ:** exchange durable `orderflow.events` y rutas explícitas para `order.created`, `stock.reserved`, `stock.rejected`. Colas durables para las suscripciones, mensajes persistentes, publisher confirms, ack manual, DLQ para mensajes no procesables. No garantizamos exactamente-una-vez a nivel de red; diseñamos efectos de negocio idempotentes bajo entrega al-menos-una-vez.

**PostgreSQL:** un contenedor con dos bases independientes (`orderflow_orders`, `orderflow_inventory`), una propiedad de cada servicio. No se hacen joins entre bases ni se conceden accesos cruzados desde la lógica. Scripts de inicialización crean ambas bases sin pasos manuales. Migraciones y seed idempotentes al arranque, con manejo de dependencias y reintentos.

**Catálogo de demostración:** fichero `seed/products.json` con al menos tres SKU. Orders utiliza una copia de referencia para validar SKU existente; Inventory inicializa el stock de los mismos SKU. Es un catálogo estático de prueba: no se expone una falsa sincronización dinámica. Documentar que en producción habría un dueño del catálogo y sincronización por eventos.

## Flujo principal

1. Usuario completa formulario y frontend hace `POST /orders`.
2. Orders valida (cliente no vacío tras normalizar espacios, SKU del catálogo, cantidad entera entre 1 y 100).
3. Orders abre transacción y persiste el pedido en `Pending` junto con `OrderCreated` en outbox.
4. La API responde HTTP 201 con objeto del pedido ya persistido. El estado inicial se preserva.
5. Publicador outbox emite `OrderCreated` a RabbitMQ y solamente marca publicado tras confirmación del broker.
6. Inventory recibe evento, abre transacción y consulta reserva por `orderId` (unicidad a nivel de base de datos).
7. Si no existía, intenta un `UPDATE ... SET disponible = disponible - cantidad WHERE sku = ... AND disponible >= cantidad RETURNING ...` atómico. Si actualiza fila, resultado `StockReserved`; de lo contrario `StockRejected`.
8. Inventory persiste la decisión y el evento correspondiente en su outbox dentro de la misma transacción, hace COMMIT y envía ACK.
9. Publicador de Inventory entrega resultado a RabbitMQ con publisher confirms.
10. Orders consume resultado, registra su `eventId` en inbox y cambia el estado únicamente si sigue en `Pending`, todo de forma transaccional. Enviar ACK tras COMMIT.
11. La API notifica por SignalR cuando procede. El panel también mantiene polling de respaldo para recuperar el estado tras desconexiones.

## Contratos mínimos

Pedido: `id` UUID, `clienteNombre` string, `sku` string, `cantidad` int, `estado` enum serializable `Pending|Confirmed|Rejected`, `creadoEn` timestamp UTC. Campos opcionales: `actualizadoEn`, `motivoRechazo` para diagnóstico y UI.

Producto inventario: `sku` string PK; `disponible` int CHECK >= 0. Usar semilla idempotente: insertar SKU faltantes sin sobreescribir stock existente.

Eventos: sobre de integración `eventId` UUID, `eventType`, `schemaVersion` (p. ej. 1), `occurredAt` UTC, `orderId`; `OrderCreated` añade `sku`,`cantidad`; resultados añaden decisión. El schemaVersion permite evolucionar contratos deliberadamente. No transmitir objetos de base de datos completos.

API:

| Método y ruta | Resultado | Error |
|---|---|---|
| `POST /orders` | 201 + pedido `Pending` | 400 validación fallida |
| `GET /orders` | 200 + lista con estados vigentes | 500 solo error no recuperable |
| `GET /orders/{id}` | 200 + detalle | 404 si no existe o identificador inválido según contrato acordado |

Usaremos JSON camelCase; UTC ISO-8601; `ProblemDetails` para errores HTTP con mensajes comprensibles. El frontend debe mostrar errores al usuario, no solo en la consola.

## Consistencia e idempotencia

- Clave de evento único: `eventId`. Clave de efecto de negocio único: `orderId`. La unicidad de `orderId` impide doble reserva incluso si llega otro evento de creación del mismo pedido con otro ID.
- Inbox de Orders: `eventId` único para descartar resultados reentregados.
- Se descuenta el stock y se graba la marca de procesamiento **en la misma transacción de Inventory**. Un fallo anterior al COMMIT revierte ambas operaciones.
- El SQL condicional deja el stock no negativo aun cuando hay múltiples consumidores simultáneos.
- Transición Orders: `Pending -> Confirmed` o `Pending -> Rejected`. Nunca se permite `Confirmed -> Rejected` ni `Rejected -> Confirmed` por una duplicación tardía.
- RabbitMQ puede duplicar mensajes. No prometemos «exactly-once delivery»; garantizamos idempotencia de la operación de negocio.

## Fallos y recuperación

| Fallo | Comportamiento observable | Recuperación |
|---|---|---|
| Broker cae antes/durante publicación | Pedido sigue `Pending` con outbox sin marcar como publicado | Publicador reintenta después de restablecer RabbitMQ |
| Publicación confirmada pero falla marcar outbox | Puede reemitirse el mismo evento | Idempotencia en Inventory evita segundo descuento |
| Worker detenido | Pedidos siguen `Pending`, no se confirman ficticiamente | Worker retoma mensajes al reiniciar |
| Worker falla antes de COMMIT | No se descuenta definitivamente; mensaje sin ACK puede redeliverarse | Transacción + ACK manual |
| Worker falla después de COMMIT pero antes de ACK | Puede redeliverarse el evento | Unicidad por `orderId` y outbox recuperable |
| Orders no consume resultado | Pedido puede continuar temporalmente `Pending` | Cola durable, reentrega y transiciones idempotentes |
| SignalR se desconecta | Interfaz puede quedar desactualizada temporalmente | Reconexión y polling que vuelve a consultar GET |
| Mensaje irrecuperable | No dejar reintentos infinitos sin diagnóstico | Registro de error con correlación, reintentos acotados y DLQ |

## Docker y despliegue

- Único comando exigible en copia limpia: `docker compose up`. Ese comando construirá tres imágenes propias (Orders API, Inventory Worker, web), levantará PostgreSQL, RabbitMQ, inicializará bases/seed y activará la interfaz.
- Publicaremos únicamente un puerto HTTP de la interfaz (propuesta `localhost:8080`) y opcionalmente la consola de RabbitMQ restringida a loopback. PostgreSQL quedará dentro de la red Docker.
- Nginx sirve el frontend y proxifica `/orders` y `/hubs/orders` al API para conservar origen único y soporte WebSocket. Mantener `/orders` público exactamente como requiere el PDF.
- Dockerfiles multi-stage; runtime sin SDK ni dependencias de compilación. `.dockerignore`, `.gitignore` y configuración con variables de entorno.
- Los valores de prueba para un entorno local no se presentarán como credenciales de producción. No guardar secretos reales en Git. Establecer una estrategia de valores de desarrollo que cumpla arranque sin pasos adicionales y explicar cómo sustituirlos en despliegue real.
- Un endpoint `/health/live` sin dependencia del broker comprueba que el proceso viva; `/health/ready` comprueba que pueda responder consultas y conectarse a su almacenamiento. Broker intermitente es una degradación tolerada por el outbox, no una razón para perder pedidos.
- Kubernetes: `Deployment`, `Service`, `ConfigMap`, liveness/readiness y requests/limits para Orders API. Una guía describe servicios dependientes y secreto externo, sin exigir clúster funcional.

## Experiencia de usuario

Estética propuesta: fondo gris muy claro, superficies blancas, tipografía legible, profundidad sutil, navegación simple y acento naranja discreto. Estados con texto e iconografía, nunca solo color. Responsive móvil/PC, teclado, foco visible, etiquetas accesibles y carga/error/empty states. Dashboard muestra resumen por estados, formulario lateral y tabla con búsqueda simple solo si no interfiere con las funciones obligatorias. Prioridad: tareas de operación y claridad, no efectos decorativos.

## Alcance excluido

Autenticación y autorización, CI/CD, pagos, conciliación financiera, catálogo editable, integración real con Shopify, Kubernetes ejecutándose y funcionalidades empresariales ajenas al ejercicio.

## Referencias oficiales verificadas

- Microsoft .NET: https://learn.microsoft.com/en-us/dotnet/core/releases-and-support
- Microsoft eventos de integración: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/integration-event-based-microservice-communications
- EF Core concurrencia: https://learn.microsoft.com/en-us/ef/core/modeling/concurrency
- RabbitMQ .NET client v7: https://www.rabbitmq.com/client-libraries/dotnet-api-guide
- RabbitMQ reliability: https://www.rabbitmq.com/docs/reliability
- Docker Compose startup: https://docs.docker.com/compose/how-tos/startup-order/
- Docker multi-stage: https://docs.docker.com/build/building/multi-stage/
- SignalR JS: https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-10.0
- K8s probes: https://kubernetes.io/docs/concepts/configuration/liveness-readiness-startup-probes/
- K8s recursos: https://kubernetes.io/docs/concepts/configuration/manage-resources-containers/
