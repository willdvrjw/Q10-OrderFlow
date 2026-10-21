# 06. Matriz de evaluación Q10 y evidencias

Cada porcentaje corresponde a la sección «Cómo evaluamos» del PDF. Los bonus se contabilizan aparte. Esta matriz contrasta cada criterio con **la implementación y las evidencias disponibles**. Los escenarios no ejecutados se distinguen expresamente.

| Criterio | Peso | Implementación existente | Evidencia y límites observados |
|---|---:|---|---|
| Arquitectura y separación de responsabilidades | 20% | Orders API, Inventory Worker, contratos y bases independientes; capas Domain/Application/Infrastructure | Separación explícita en proyectos y bases. El alta de productos usa HTTP interno idempotente, sin prometer transacción distribuida; ver `docs/01_ARQUITECTURA.md` y `docs/04_GUIA_DE_DEFENSA.md`. |
| Mensajería y consistencia | 20% | RabbitMQ, outbox en ambos publicadores, inbox/idempotencia, ACK manual, SQL condicional | Prueba de idempotencia y concurrencia con PostgreSQL real; recuperación manual del broker y Worker |
| Frontend | 15% | React TypeScript; formularios, estados y errores visibles; SignalR/polling | Capturas escritorio y móvil 530 × 710; creación y cambios de estado, WebSocket 101, polling al bloquear SignalR |
| Docker y despliegue | 15% | Dockerfiles propios multi-stage, Compose, Postgres+RabbitMQ, seed y health checks | Clon limpio en el mismo Codespace, compilación y reserva verificadas; requirió ajuste temporal de firewall del host. No probado en otro equipo |
| Calidad del código | 10% | Código cohesivo, comentarios de decisiones complejas, contratos y logs estructurados | Código revisado; guía de defensa corregida con rutas existentes. No hay análisis externo de calidad automatizado |
| Tests | 10% | Siete tests backend: tres de dominio y cuatro contra PostgreSQL real | Verificado en Codespaces: **7 aprobados, 0 fallidos** (21/10/2026). Las dos pruebas añadidas verifican productos dinámicos/idempotencia. |
| README y comunicación | 10% | Inicio rápido Docker y Codespaces, navegador, puerto configurable, fallos, arquitectura, trade-offs y «Qué haría distinto con más tiempo» | Documentación revisada; el tiempo real de un evaluador independiente no se ha medido. La demo pública permanente no está desplegada. |

## Bonus implementados y alcance de su comprobación

- **Kubernetes (+10):** Deployment, Service, ConfigMap, startup/liveness/readiness y requests/limits implementados. `kubectl kustomize k8s` ensambló los tres recursos sin errores; no se realizó validación contra un clúster. Ver `docs/08_KUBERNETES.md`.
- **.NET (+5):** backend en .NET 10 LTS, uso idiomático de ASP.NET Core y Worker Service.
- **SignalR (sin número especificado):** actualización push de pedidos con reconexión y polling de recuperación.

## Requisitos explícitamente fuera de alcance

No construir CI/CD, un clúster Kubernetes funcional, alta cobertura de tests ni funcionalidades empresariales ajenas a la prueba. Una interfaz profesional y accesible es valiosa mientras no comprometa entrega ni estabilidad.

## Criterio para aprobar la entrega

La implementación se compiló y se ejecutó con Docker en Codespaces; se probaron siete tests backend, persistencia, fallos prioritarios, productos dinámicos y clon con bases nuevas. La portabilidad a un host Docker distinto y la ejecución real sobre clúster Kubernetes no se comprobaron. Ver el registro detallado `docs/09_EVIDENCIAS_VALIDACION.md`.
