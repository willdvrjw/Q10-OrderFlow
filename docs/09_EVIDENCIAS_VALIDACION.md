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

- [ ] Parar y recuperar Inventory Worker sin pérdida del pedido.
- [ ] Parar y recuperar RabbitMQ sin pérdida del evento Outbox.
- [ ] Reiniciar contenedores con volúmenes existentes y comprobar que stock/pedidos persisten.
- [ ] Verificar errores `400` desde la API y su visibilidad desde el formulario.
- [ ] Verificar interfaz en navegador y versión responsive.
- [ ] Verificar SignalR y polling frente a desconexión.
- [ ] Validar el ensamblado de manifiestos Kubernetes y sus referencias.
- [ ] Ejecutar el arranque obligatorio desde copia limpia en un host Docker sin ajustes especiales.
- [ ] Revisar la consistencia final entre README, código y guía de defensa.

Los elementos pendientes no deben presentarse como pruebas realizadas.
