# Q10-OrderFlow

Evaluación Fullstack: registro de pedidos, reserva asíncrona de inventario y panel de operaciones.

**Estado del proyecto:** versión funcional comprobada en GitHub Codespaces mediante Docker Compose.

- **Backend:** Orders API e Inventory Worker en .NET 10, RabbitMQ y PostgreSQL; pedidos `Pending`, `Confirmed` y `Rejected`.
- **Robustez:** Outbox transaccional, reintentos, idempotencia por pedido y descuento atómico; recuperación verificada ante detenciones de RabbitMQ y Worker.
- **Pruebas:** cinco pruebas backend aprobadas, incluidas dos de integración con PostgreSQL real.
- **Frontend:** React + TypeScript con validación, filtrado, SignalR y recuperación por polling. Dependencias fijadas en `web/package-lock.json` e instaladas con `npm ci`.
- **Despliegue:** compilación, seed y creación/reserva comprobados en un clon independiente dentro del mismo Codespace; bonus Kubernetes ensamblado sintácticamente, no desplegado en un clúster.

Los resultados y límites de verificación están documentados en [evidencias de validación](docs/09_EVIDENCIAS_VALIDACION.md).

## Arranque reproducible

Requisito del evaluador: Docker Engine y Docker Compose. No hace falta instalar .NET ni Node.js localmente porque los Dockerfiles compilan los servicios.

Desde la raíz del repositorio:

~~~bash
docker compose up
~~~

- Panel web: http://localhost:8080
- Estado de API: http://localhost:8080/health/ready
- Catálogo: http://localhost:8080/products
- Pedidos: POST /orders, GET /orders, GET /orders/{id}
- SignalR: /hubs/orders; polling de respaldo cada cinco segundos

Si prefieres dejar el arranque en segundo plano:

~~~bash
docker compose up -d --build
docker compose ps
~~~

Los datos PostgreSQL y RabbitMQ usan volúmenes persistentes. No ejecutar un borrado de volúmenes si se desea conservar pedidos e inventario.

## Catálogo de demostración

| SKU | Producto | Stock inicial |
|---|---|---:|
| ABC-01 | Cuaderno académico | 100 |
| XYZ-02 | Bolígrafo profesional | 25 |
| TEST-03 | Agenda de escritorio | 3 |

Las semillas se insertan solo cuando los SKU faltan; no reinician cantidades ya consumidas.

Ejemplo de petición:

~~~bash
curl -i -X POST http://localhost:8080/orders \
  -H "Content-Type: application/json" \
  -d '{"clienteNombre":"Prueba Q10","sku":"ABC-01","cantidad":2}'
~~~

El POST devuelve HTTP 201 con estado Pending, aunque la reserva aún no terminó. Inventory Worker determina Confirmed o Rejected por evento. Nombre vacío, SKU inexistente o cantidad fuera de 1 a 100 producen HTTP 400 con errores visibles para el frontend.

## Gestión de existencias (ampliación de la prueba)

La página incorpora una sección **Inventario disponible**, con consulta del stock actual, selección de SKU y dos operaciones: **Agregar unidades** y **Establecer cantidad total**. Inventory es el único dueño de la tabla `stock_items`; Orders API nunca actualiza su base.

- `GET /inventory`: consulta existencias actuales desde Inventory Worker (HTTP interno).
- `POST /inventory/adjustments`: envía `sku`, `operacion` (`agregar` o `establecer`), `cantidad` y `existenciaEsperada`.
- El ajuste se realiza mediante una sola sentencia SQL condicional: si las existencias cambiaron desde que el operador consultó, devuelve HTTP **409 Conflict** y no sobrescribe la reserva concurrente.
- La operación de agregar requiere al menos una unidad; establecer permite cero. El navegador actualiza las existencias cada diez segundos. La imagen de portada se encuentra en `web/public/images/centro-logistico-orderflow.webp`.

**Alcance de seguridad:** este panel administrativo es una ampliación **solo para demostración**, sin autenticación, autorización ni historial de ajustes. No debe exponerse en producción ni publicar el puerto 8080 de la demostración a usuarios no confiables. Un despliegue real exige autenticación administrativa, roles y auditoría de movimientos.

**Estado de la ampliación:** código en rama de pruebas `feat/inventory-and-ui-refinement`, sujeto a compilación y validación antes de integrarlo en `main`. Los resultados ya verificados de la versión anterior se registran por separado.

## Cinco tests backend, un comando

~~~bash
docker compose --profile tests run --build --rm backend-tests
~~~

Este comando utiliza el perfil opcional de pruebas y no altera el arranque normal con docker compose up. Ejecuta tres tests de dominio y dos sobre PostgreSQL real:

1. Validación de nombre, SKU y cantidad.
2. Pedido creado en Pending, con transición única a Confirmed.
3. Pedido rechazado como estado terminal.
4. Idempotencia: reenviar OrderCreated con el mismo OrderId y EventId distinto no descuenta inventario dos veces.
5. Concurrencia: dos reservas distintas sobre el mismo SKU no generan stock negativo y solo una puede descontar cuatro de cinco unidades.

**Aislamiento:** las dos pruebas PostgreSQL crean una base temporal distinta por test y la eliminan al finalizar; no modifican los pedidos de demostración. Requieren permisos de crear y eliminar bases sobre el servidor PostgreSQL de desarrollo. **Resultado verificado en Codespaces el 20/10/2026: cinco aprobadas, ninguna fallida.**

Para compilar sin iniciar servicios, ejecutar dotnet build OrderFlow.slnx. Los tests de PostgreSQL requieren conexión administrativa de pruebas mediante ORDERFLOW_TEST_POSTGRES; el comando con perfil Docker la configura automáticamente.

## Arquitectura

- Orders API (.NET 10): endpoints, reglas y persistencia de pedidos, Outbox transaccional de OrderCreated y consumidor de resultados.
- Inventory Worker (.NET 10): almacenamiento separado, reserva atómica condicionada por stock, clave única OrderId, Outbox de resultados.
- RabbitMQ: colas durables, confirmaciones de publicación, ACK tras confirmar la transacción, DLQ para mensajes inválidos.
- PostgreSQL: dos bases lógicas propiedad de servicios independientes, dentro de un contenedor.
- React, TypeScript y Nginx: formulario, errores visibles, búsqueda, estados, SignalR y polling.

No se promete exactly-once delivery sobre RabbitMQ; los consumidores deben tolerar reentregas y garantizar que el efecto de negocio sea único por OrderId.

### Escenarios de fallo y alcance de su verificación

| Fallo | Comportamiento previsto |
|---|---|
| Broker caído | Verificado: el pedido permaneció Pending y se confirmó tras recuperar el broker. |
| Worker caído | Verificado: el pedido permaneció Pending y se confirmó tras reiniciar Inventory Worker. |
| Fallo antes de COMMIT | Diseño transaccional implementado; no se forzó el fallo en ese instante. |
| Fallo tras COMMIT y antes de ACK | Idempotencia con PostgreSQL comprobada; no se forzó una caída justo entre COMMIT y ACK. |
| Fallo de SignalR | Verificado: con el WebSocket bloqueado, los indicadores se actualizaron mediante polling. |

## Trade-offs

- EnsureCreated permite inicializar la demostración; para producción usaríamos migraciones versionadas.
- Un solo contenedor PostgreSQL facilita reproducibilidad, pero no implica alta disponibilidad.
- RabbitMQ.Client expone explícitamente reintentos, ACK y confirms, a cambio de más lógica propia.
- El catálogo fijo simplifica el ejercicio; en producción tendría un servicio propietario de datos y sincronización.
- Las variables de entorno incluyen credenciales de prueba por defecto para que docker compose up funcione sin configuración manual. No reutilizarlas en producción. El archivo .env local no se versiona.

## Qué haría distinto con más tiempo

Reemplazar inicialización de esquemas por migraciones; introducir privilegios mínimos y gestión de secretos; ampliar métricas, trazas, alarmas, políticas de DLQ y pruebas de fallos; administrar productos mediante un catálogo independiente y agregar controles de seguridad adecuados a producción.

## Organización y defensa técnica

- src/Orders.Api: dominio, validaciones, API, outbox, consumidor y SignalR.
- src/Inventory.Worker: stock, transacciones, idempotencia y consumo de eventos.
- src/OrderFlow.Contracts: contratos de eventos.
- src/OrderFlow.Messaging: transporte y topología RabbitMQ.
- tests/OrderFlow.Tests: cinco pruebas backend, dos con PostgreSQL.
- web: panel React.
- infra: inicialización de bases y proxy Nginx.
- k8s: bonus con Deployment, Service, ConfigMap, probes y límites **implementado; aún no validado en un clúster**. Ver `docs/08_KUBERNETES.md`.
- docs/01_ARQUITECTURA.md: flujo y fallos.
- docs/02_DECISIONES_TECNICAS.md: decisiones y costes.
- docs/03_PLAN_DE_VALIDACION.md: aceptación y evidencias.
- docs/04_GUIA_DE_DEFENSA.md: fundamentos y rutas para modificar código.
- docs/05_CONVENCIONES_Y_CAMBIOS.md: convenciones.
- docs/06_MATRIZ_EVALUACION.md: criterios y ponderaciones.
- docs/07_DISENO_INTERFAZ.md: UX accesible y responsive.
- docs/08_KUBERNETES.md: alcance, decisiones y límites del bonus.
- docs/09_EVIDENCIAS_VALIDACION.md: registro de resultados reales.

**Alcance de validación:** el sistema se ejecutó desde una copia nueva dentro de GitHub Codespaces, con volúmenes independientes. El firewall específico de ese entorno requirió un ajuste local de conectividad entre contenedores. No se ha probado todavía en un host Docker diferente. La emulación responsive se verificó a 530 × 710; no se ensayó 390 × 844. Kubernetes se verificó mediante Kustomize, sin clúster. Ver `docs/09_EVIDENCIAS_VALIDACION.md`.
