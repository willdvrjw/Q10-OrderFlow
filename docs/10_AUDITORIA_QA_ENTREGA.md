# 10. Auditoría QA final — Q10-OrderFlow

Esta auditoría contrasta el código público con el enunciado de la prueba y diferencia implementación, evidencias observadas y límites. Los pesos son los del enunciado; no son calificaciones atribuidas por Q10.

## Matriz de evaluación

| Área | Peso | Implementación | Evidencia y límites |
|---|---:|---|---|
| Arquitectura | 20 % | Orders API, Inventory Worker, Contracts, Messaging y dos bases separadas | Separación real. El alta dinámica usa HTTP interno entre servicios sin prometer transacción distribuida. |
| Mensajería y consistencia | 20 % | RabbitMQ, Outbox/Inbox, ACK tras COMMIT, reservas SQL condicionales y DLQ | Idempotencia, concurrencia y recuperación de broker/Worker comprobadas. No se forzó fallo exactamente entre COMMIT y ACK. |
| Frontend | 15 % | React/TypeScript; validación visible, filtros, estados, SignalR, polling | Flujos y responsive aprobados manualmente. No se certificaron individualmente Safari, Firefox y Edge. |
| Docker y despliegue | 15 % | Compose, Dockerfiles multi-stage, semilla, redes y volúmenes | Ejecución observada en Codespaces; su host necesitó ajuste de firewall. Arranque limpio en otro host no verificado. |
| Calidad | 10 % | Responsabilidades, validación backend, configuración ambiental, manejo de errores | Endpoints administrativos sin autenticación por decisión de alcance del ejercicio; prohibido exponer demo pública sin protección. |
| Tests | 10 % | 3 de dominio y 4 de integración PostgreSQL | **7 aprobados, 0 fallidos**; cinco son originales y dos cubren la ampliación de catálogo. |
| README y comunicación | 10 % | Guía multiplataforma, Codespaces, comandos, fallos, trade-offs y mejoras | Inicio bien explicado, pero no se promete tiempo o compatibilidad universal medidos. |

**Bonus:** .NET 10, SignalR + polling, manifiestos Kubernetes de Orders con probes, Service, Deployment, ConfigMap y recursos. Kubernetes no se desplegó en un clúster.

## Criterios obligatorios

1. **Arranque único:** docker compose up levanta web, los dos servicios, PostgreSQL y RabbitMQ. Los tres SKU se cargan automáticamente, sin requerir .NET ni Node.js fuera de Docker.
2. **Pedidos:** POST /orders devuelve 201 y Pending para datos válidos. GET /orders y GET /orders/{id} consultan los pedidos; el worker decide Confirmed/Rejected después de reservar existencias.
3. **Validación:** cliente no vacío, SKU conocido, cantidad 1–100; los errores HTTP 400 se muestran al usuario. Pedido válido sin stock puede ser rechazado después, pero se registra primero.
4. **Idempotencia:** un mismo OrderId no descuenta inventario dos veces; colas durables y eventos Outbox resisten fallos transitorios.
5. **Tests:** docker compose --profile tests run --build --rm backend-tests ejecuta la suite aislada de 7 pruebas con PostgreSQL real.
6. **Frontend:** formulario de pedidos, lista con estado actualizado, mensajes visibles y polling de respaldo. Alta de productos e inventario son extensiones que no alteran las transiciones requeridas.
7. **Documentación:** arquitectura, trade-offs, arranque, pruebas, fallos y sección “Qué haría distinto con más tiempo”.

## Acceso para evaluadores

**Código:** https://github.com/willdvrjw/Q10-OrderFlow

**Codespace propio de cada evaluador:** https://codespaces.new/willdvrjw/Q10-OrderFlow?quickstart=1

**Docker local:** descargar/clonar, abrir la carpeta con compose.yaml, ejecutar docker compose up, visitar http://localhost:8080 y confirmar disponibilidad en http://localhost:8080/health/ready.

El repositorio público **no significa** un servidor web permanente. Codespaces requiere GitHub y cuota; su puerto personal se mantiene privado por defecto. No hacer público un sistema con endpoints administrativos sin autenticación ni utilizar información real de terceros. Los volúmenes son persistentes; eliminar volúmenes destruye datos.

## Evidencias obtenidas

- **7/7 pruebas** exitosas tras resolver el firewall particular de Codespaces.
- Producto nuevo creado desde la interfaz, usado en pedido, y existencias actualizadas; confirmado manualmente por el usuario.
- SKU repetido bloqueado por UI y por Orders API con **HTTP 409**, sin cambios en productos, inventario o pedidos.
- Reinicio de Orders, Inventory y web: comparación byte a byte SIN CAMBIOS en /products, /inventory y /orders.
- Nginx con DNS interno de Docker: HTTP 200 en salud, catálogo e inventario tras recrear servicios. En la última QA portable (21/10), hubo errores transitorios y dos reinicios de cada backend antes de recuperar HTTP 200 en `/health/ready`; no se presenta la primera ejecución como libre de incidencias.
- Diseño móvil y escritorio aprobado expresamente por el usuario; no se simuló la creación de cientos de productos.

## Pendientes que no deben presentarse como aprobados

- Arranque limpio de Windows/macOS/Linux fuera de Codespaces y tiempo real de inicio; revisión de Chrome/Edge/Firefox/Safari en diferentes SO.
- Revisión del nuevo healthcheck con Docker desde el entorno del evaluador, y del puerto local alternativo.
- Inyección de caída entre COMMIT y ACK, negociación de WebSocket tras el último ajuste de Nginx y polling de inventario con SignalR bloqueado.
- Investigación de la advertencia de canal asíncrono de Chrome en un navegador sin extensiones.
- Prueba con centenares de SKU, entorno público hospedado y despliegue Kubernetes real.

La QA de código y el README no sustituyen las ejecuciones externas. El informe separa las pruebas observadas de los controles pendientes y conserva los contratos del dominio.

Para evidencias con fechas y comandos, consultar [09_EVIDENCIAS_VALIDACION.md](09_EVIDENCIAS_VALIDACION.md). Para controles pendientes, consultar [03_PLAN_DE_VALIDACION.md](03_PLAN_DE_VALIDACION.md).
