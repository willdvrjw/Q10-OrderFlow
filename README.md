# Q10-OrderFlow

Prueba técnica de arquitectura Fullstack para el proceso de Q10. Proyecto de comercio electrónico con procesamiento de pedidos y reserva asíncrona de inventario.

> **Estado actual: ARQUITECTURA DEFINIDA, IMPLEMENTACIÓN PENDIENTE.** Este archivo no afirma que el sistema ya compile, que Docker Compose funcione o que sus pruebas hayan pasado. Las instrucciones ejecutables se documentarán y comprobarán cuando exista la implementación.

## Objetivo de negocio

- Registrar pedidos desde un panel web.
- Validar nombre, SKU existente y cantidad (1 a 100).
- Crear cada pedido inicialmente en `Pending`.
- Procesar la reserva de inventario de forma asíncrona.
- Transicionar a `Confirmed` si hay stock y a `Rejected` si no lo hay.
- Visualizar las transiciones sin depender exclusivamente de recargar manualmente la página.
- Evitar descuentos repetidos por eventos duplicados y stock negativo ante concurrencia.

## Stack seleccionado

- **Backend:** C# / ASP.NET Core 10 y .NET Worker Service 10.
- **Base de datos:** PostgreSQL + Entity Framework Core / Npgsql, con dos bases de datos lógicas propiedad de servicios distintos.
- **Mensajería:** RabbitMQ y librería oficial RabbitMQ.Client 7.x.
- **Frontend:** React + TypeScript + Vite; estilos CSS modulares.
- **Actualizaciones:** SignalR, con recuperación y polling periódico como respaldo.
- **Contenedores:** Dockerfiles multi-stage + Docker Compose.
- **Bonus infraestructura:** manifiestos de Kubernetes para Orders API.
- **Pruebas:** xUnit con pruebas de dominio y de persistencia.

## Organización

- `src/Orders.Api/`: API REST de pedidos, almacenamiento de estados, consumidor de resultados y publicación de eventos.
- `src/Inventory.Worker/`: reservas, idempotencia y publicación de resultados.
- `src/OrderFlow.Contracts/`: contratos públicos versionados de eventos, sin lógica de negocio.
- `src/OrderFlow.Messaging/`: conexiones, publicador con confirmaciones y utilidades transversales de RabbitMQ.
- `tests/OrderFlow.Tests/`: cinco tests backend sobre requisitos críticos.
- `web/`: panel responsivo de operaciones.
- `infra/postgres/`: inicialización automática de las dos bases de datos.
- `infra/nginx/`: proxy web y compatibilidad con SignalR.
- `k8s/`: bonus Kubernetes.
- `seed/`: catálogo inicial estático de al menos tres productos.
- `docs/`: decisiones, contrato de arquitectura, validación y guía de defensa.

## Documentos de consulta

1. `docs/01_ARQUITECTURA.md` — arquitectura completa, modelo de datos, flujos y tratamiento de fallos.
2. `docs/02_DECISIONES_TECNICAS.md` — justificación de cada decisión y sus trade-offs.
3. `docs/03_PLAN_DE_VALIDACION.md` — aceptación, cinco tests críticos y pruebas manuales de resiliencia.
4. `docs/04_GUIA_DE_DEFENSA.md` — fundamentos que el candidato debe comprender y poder modificar en vivo.
5. `docs/05_CONVENCIONES_Y_CAMBIOS.md` — rutas y estándares de comentarios, código, configuración y documentación.
6. `docs/06_MATRIZ_EVALUACION.md` — alineación uno a uno con los porcentajes y bonus de Q10.
7. `docs/07_DISENO_INTERFAZ.md` — especificación visual y UX del panel.

## Alcance

Se evita añadir autenticación, CI/CD o integraciones con Shopify, pues no son exigidas por la prueba y añadirían complejidad sin servir al flujo de reserva. El diseño del panel será visualmente cuidado y accesible sin sacrificar la robustez del backend.

## Pendiente antes de entregar

La implementación completa, compilación, `docker compose up`, cinco tests backend, simulaciones de fallos, repositorio Git con historial real y validación desde copia limpia. **No entregar mientras estos puntos no hayan sido verificados.**
