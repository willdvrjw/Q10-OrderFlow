# 06. Matriz de evaluación Q10 y evidencias

Cada porcentaje corresponde a la sección «Cómo evaluamos» del PDF. Los bonus se contabilizan aparte. Esta matriz traduce el criterio del evaluador en evidencias **por generar y verificar**, no afirmaciones anticipadas.

| Criterio | Peso | Implementación diseñada | Evidencia que hay que obtener |
|---|---:|---|---|
| Arquitectura y separación de responsabilidades | 20% | Orders API, Inventory Worker, contratos y bases independientes; capas Domain/Application/Infrastructure | Revisar grafo de dependencias, README y recorrido del código |
| Mensajería y consistencia | 20% | RabbitMQ, outbox en ambos publicadores, inbox/idempotencia, ACK manual, SQL condicional | Duplicación de OrderCreated, caída de broker, caída de worker, simultaneidad |
| Frontend | 15% | React TypeScript; formularios, estados y errores visibles; SignalR/polling | Capturas y prueba manual desktop/móvil del ciclo completo |
| Docker y despliegue | 15% | Dockerfiles propios multi-stage, Compose, Postgres+RabbitMQ, seed y health checks | `docker compose up` en copia limpia sin pasos manuales adicionales |
| Calidad del código | 10% | Código cohesivo, comentarios de decisiones complejas, contratos y logs estructurados | Revisión estática y explicación de cada método importante |
| Tests | 10% | Exactamente 5 tests backend dirigidos a validación, transiciones, stock, idempotencia y concurrencia | Un solo comando documentado, todos pasando |
| README y comunicación | 10% | Instalación, puertos, arquitectura, trade-offs, fallos y «Qué haría distinto con más tiempo» | Evaluador nuevo entiende y levanta en menos de 10 minutos |

## Bonus planificados desde la arquitectura

- **Kubernetes (+10):** Deployment, Service, ConfigMap, liveness/readiness y requests/limits para Orders API; no se promete clúster activo.
- **.NET (+5):** backend en .NET 10 LTS, uso idiomático de ASP.NET Core y Worker Service.
- **SignalR (sin número especificado):** actualización push de pedidos con reconexión y polling de recuperación.

## Requisitos explícitamente fuera de alcance

No construir autenticación/autorización, CI/CD, cluster Kubernetes funcional, alta cobertura de tests ni diseño visual suntuoso. Una interfaz profesional y accesible es valiosa mientras no comprometa entrega ni estabilidad.

## Criterio para aprobar la entrega

No basta con que el código esté escrito: confirmar en un entorno con Docker los seis criterios de aceptación de la página 4 del PDF, el comando de tests, la persistencia de datos y al menos las fallas prioritarias. Documentar por separado cualquier bonus solo diseñado pero no validado sintácticamente.
