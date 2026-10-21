# 09. Evidencias de validación — Q10-OrderFlow

Registro de resultados observados en GitHub Codespaces el 20 de octubre de 2026. Las verificaciones se mantienen separadas de las características solamente implementadas.

## Compilación .NET 10

- `dotnet build OrderFlow.slnx`: cinco proyectos compilados sin errores.
- Revisión posterior de `RabbitMQ.Client 7.2.2`: se corrigió `DeliveryMode` para usar `DeliveryModes.Persistent`. La compilación posterior terminó satisfactoriamente.

## Docker Compose

- `docker compose config --quiet`: completado sin error.
- `docker compose up -d --build`: construyó `orders-api`, `inventory-worker` y `web`.
- `docker compose ps`: cinco contenedores activos; PostgreSQL 16 y RabbitMQ 4.1 declarados saludables.

### Incidencia de red específica de Codespaces

En este Codespace, `iptables-legacy` tenía política `FORWARD DROP`, sin regla de paso entre los contenedores del puente generado. PostgreSQL respondía localmente pero las conexiones desde otros contenedores expiraban. Se agregó temporalmente una regla local al host; luego una consulta `SELECT 1` desde un contenedor adicional respondió correctamente y los servicios arrancaron tras reiniciarlos.

**Este ajuste del host no es requisito de OrderFlow ni se versiona en Compose.** No se debe confundir con una verificación de despliegue en otro host. Está pendiente validar `docker compose up` desde un clon limpio en otro entorno Docker.

## Endpoints y pruebas de negocio observadas

| Ensayo | Resultado |
|---|---|
| `GET /health/ready` | HTTP 200, `{"status":"ready"}` |
| `GET /products` | HTTP 200, tres productos del seed |
| `GET /orders` inicial | HTTP 200, `[]` |
| `POST /orders` con `ABC-01`, cantidad 2 | HTTP 201, estado inicial `Pending` |
| Consulta posterior del pedido | `Confirmed`, aproximadamente 1,57 s después |
| `POST /orders` con `TEST-03`, cantidad 4 | Creación válida seguida de `Rejected` |
| Motivo del rechazo | `Inventario insuficiente.` |
| Stock de PostgreSQL tras ambos pedidos | `ABC-01=98`, `TEST-03=3`, `XYZ-02=25` |

## Ensayo verificado: recuperación de Inventory Worker

**Fecha observada:** 21/10/2026, UTC (20/10/2026 en Colombia).

1. Se detuvo únicamente el servicio con `docker compose stop inventory-worker`.
2. Con el consumidor detenido se creó el pedido `f069d769-005e-4223-9e30-2ae03992abff`, SKU `ABC-01`, cantidad 1. La API devolvió HTTP `201 Created`, estado `Pending`.
3. Después de tres segundos, `GET /orders` mantuvo el pedido en `Pending`.
4. Se reanudó el consumidor con `docker compose start inventory-worker`.
5. Una consulta posterior mostró **el mismo pedido** con estado `Confirmed`, sin volver a enviarlo.

**Alcance de la evidencia:** recuperación automática de un pedido pendiente tras detener y arrancar el consumidor. El ensayo no comprueba por sí solo la recuperación del broker, la protección ante ACK perdido ni que no haya duplicados si se vuelve a entregar el mismo evento.

## Ensayo verificado: recuperación de RabbitMQ

**Fecha observada:** 21/10/2026 UTC (20/10/2026 en Colombia).

1. Se detuvo RabbitMQ con `docker compose stop rabbitmq`.
2. Durante la desconexión, Orders API aceptó con HTTP 201 el pedido `bca788f1-45dc-4e73-887a-4982d946095e` por dos unidades de `XYZ-02`, inicialmente en `Pending`.
3. Una consulta posterior confirmó que el mismo pedido permanecía `Pending` mientras el broker estaba detenido.
4. Tras `docker compose start rabbitmq` y esperar, `GET /orders` mostró el mismo pedido en `Confirmed`, sin reenviar manualmente la solicitud.
5. PostgreSQL confirmó stock `XYZ-02 = 23` (inicialmente 25). También mostró `ABC-01 = 97` y `TEST-03 = 3`, coherentes con las pruebas anteriores.

**Alcance de la evidencia:** creación de pedidos sin disponibilidad del broker, retención del trabajo pendiente, reanudación automática y descuento de inventario correcto. El ensayo no fuerza una caída exactamente entre publisher confirm y marcado de Outbox, ni prueba todos los casos de ACK perdido.

## Pruebas backend automatizadas

Comando ejecutado:

~~~bash
docker compose --profile tests run --build --rm backend-tests
~~~

Resultado observado:

~~~text
Test Run Successful.
Total tests: 5
     Passed: 5
 Total time: 9.1030 Seconds
Test summary: total: 5, failed: 0, succeeded: 5, skipped: 0
~~~

Las tres pruebas de dominio comprobaron validaciones y estados terminales.
Dos pruebas de integración utilizaron **PostgreSQL real y bases temporales aisladas**:

- Idempotencia: el mismo `OrderId` con `EventId` diferente no descontó dos veces.
- Concurrencia: de dos reservas de cuatro unidades con cinco disponibles, exactamente una reservó; la otra rechazó, sin stock negativo.

## Pendiente de aprobación

- [x] Parar y recuperar Inventory Worker sin pérdida del pedido (ensayo REST + Compose verificado).
- [x] Parar y recuperar RabbitMQ sin pérdida del evento Outbox (pedido bca788f1 confirmado tras recuperación, stock XYZ-02 = 23).
- [ ] Reiniciar contenedores con volúmenes existentes y comprobar que stock/pedidos persisten.
- [ ] Verificar errores `400` desde la API y su visibilidad desde el formulario.
- [ ] Verificar interfaz en navegador y versión responsive.
- [ ] Verificar SignalR y polling frente a desconexión.
- [ ] Validar el ensamblado de manifiestos Kubernetes y sus referencias.
- [ ] Ejecutar el arranque obligatorio desde copia limpia en un host Docker sin ajustes especiales.
- [ ] Revisar la consistencia final entre README, código y guía de defensa.

Los elementos pendientes no deben presentarse como pruebas realizadas.
