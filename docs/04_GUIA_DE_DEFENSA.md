# 04. Guía de defensa técnica en entrevista

Las respuestas siguientes son intenciones arquitectónicas que deben poder demostrarse sobre el código real. No memorizar sin entender; una evaluación en vivo puede pedir modificaciones.

## Qué debes poder responder y demostrar

**¿Por qué se crea Pending y no Confirmed?** Porque crear un pedido no equivale a reservar inventario. La reserva se resuelve después, por evento.

**¿Qué devuelve POST?** 201 Created con pedido persistido en Pending; la confirmación no se espera síncronamente.

**¿Qué distingue 400 de Rejected?** 400 es petición inválida (SKU fuera del catálogo, cantidad fuera del rango o nombre vacío); Rejected es un pedido válido al que le faltó stock.

**¿Por qué un servicio no consulta la base del otro?** Cada servicio es dueño de su estado, y comunica hechos mediante eventos; evita acoplamiento al esquema y a transacciones entre servicios.

**¿Qué es consistencia eventual aquí?** Durante un intervalo el pedido existe en Pending aunque Inventory ya esté procesando; la vista converge al resultado confirmado o rechazado cuando el evento es consumido.

**¿Qué pasa con RabbitMQ apagado?** La orden y el evento quedan persistidos en Orders; un despachador reintenta su publicación. Nunca se confunde publicación pendiente con pedido rechazado.

**¿Por qué outbox?** No existe una transacción única entre PostgreSQL y RabbitMQ. Guardar estado y evento en la misma base local evita que se pierda el evento tras un COMMIT exitoso.

**¿Qué pasa si el broker recibe el evento pero el despachador muere antes de marcarlo enviado?** El mismo evento puede publicarse de nuevo; el consumidor es idempotente.

**¿Cómo garantizamos la idempotencia?** Registro único de efecto por `orderId` bajo transacción y clave única. Duplicar un `eventId` o recibir otro evento de creación para el mismo pedido no vuelve a descontar.

**¿Cómo garantizamos stock no negativo?** `UPDATE` atómico con condición `disponible >= cantidad`; el número de filas actualizadas determina si se reservó.

**¿Por qué ACK manual?** RabbitMQ no debe retirar el mensaje antes de que se confirme la transacción que lo procesa. Un fallo permite redelivery.

**¿Qué es una dead-letter queue?** Una cola separada para mensajes que no se pueden procesar y requieren diagnóstico; evita bucles silenciosos.

**¿Por qué SignalR junto a polling?** SignalR aporta baja latencia; polling garantiza que un cliente recupere el estado luego de pérdidas de conexión o notificaciones.

**¿Por qué Docker Compose?** Reproduce servicios, red, orden de inicio, persistencia y semillas sin instalaciones locales independientes.

**¿Qué cambia en Kubernetes?** Incluimos Deployment (una réplica), Service interno, ConfigMap, startup/liveness/readiness y CPU/memoria. El Secret se gestiona fuera de Git y las bases/broker requieren servicios independientes. Se usa una réplica porque Outbox y SignalR aún no incorporan coordinación multinodo. Ver `docs/08_KUBERNETES.md`.

**¿Qué harías diferente en producción?** Catálogo con servicio dueño, telemetría centralizada, secretos gestionados, almacenamiento/broker de alta disponibilidad, migraciones supervisadas, pruebas de caos y políticas de seguridad.

## Rutas previstas para modificaciones en vivo

Estas rutas son un **contrato de organización propuesto**, no archivos implementados aún:

| Modificación solicitada | Ruta prevista | Cómo verificar |
|---|---|---|
| Regla de cliente/cantidad/SKU | `src/Orders.Api/Application/Orders/OrderValidator.cs` | Tests de validación y HTTP 400 |
| POST/GET de pedidos | `src/Orders.Api/Endpoints/OrdersEndpoints.cs` | Curl/UI y códigos HTTP |
| Estados y transiciones | `src/Orders.Api/Domain/Order.cs` | Tests transición confirmada/rechazada |
| Publicación diferida | `src/Orders.Api/Infrastructure/Outbox/OrdersOutboxDispatcher.cs` | Parar broker, crear, reiniciar |
| Consumidor de resultados | `src/Orders.Api/Infrastructure/Messaging/StockResultConsumer.cs` | Duplicar evento/estado único |
| Descuento atómico | `src/Inventory.Worker/Infrastructure/StockReservationRepository.cs` | Tests de concurrencia |
| Idempotencia del worker | `src/Inventory.Worker/Application/OrderCreatedHandler.cs` | Reentrega del mismo pedido |
| Publicación de reservas | `src/Inventory.Worker/Infrastructure/Outbox/InventoryOutboxDispatcher.cs` | Reanudar después de error |
| Catálogo de prueba | `seed/products.json` | Reiniciar sin sobrescribir stock |
| Actualización de pedidos | `web/src/hooks/useOrdersLive.ts` | Desconectar SignalR |
| Formulario | `web/src/components/OrderForm.tsx` | Validaciones y errores visibles |
| Tokens de estilo | `web/src/styles/tokens.css` | Desktop/móvil, contraste |
| Configuración de puertos | `compose.yaml` y `infra/nginx/default.conf` | `docker compose up` y GET `/orders` |
| Despliegue de bonus | `k8s/orders-api-deployment.yaml` | Validación sintáctica YAML |

Cada ruta será confirmada una vez generado el código y deberá reflejarse en este documento si cambia.
