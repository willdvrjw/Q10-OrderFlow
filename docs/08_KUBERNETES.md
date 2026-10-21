# 08. Bonus Kubernetes — diseño de despliegue

## Alcance del PDF

El bonus pide `Deployment`, `Service`, `ConfigMap`, probes de salud,
límites de recursos y una explicación breve del despliegue completo.
**No exige un clúster funcionando.** Los manifiestos de `k8s/` describen
únicamente Orders API y no se presentan como infraestructura ya desplegada.

## Recursos incluidos y decisiones

- **Deployment** `orderflow-orders-api`: un contenedor ASP.NET Core 10, un
  puerto HTTP 8080, `replicas: 1`, recursos reservados (100m/128Mi) y
  límites (500m/512Mi). Los valores se ajustarían con métricas reales.
- **Service** ClusterIP por puerto 80 hacia el 8080 del contenedor:
  servicio interno para un eventual proxy/Ingress, no exposición pública.
- **ConfigMap**: configuración no confidencial del proceso y host/puerto
  RabbitMQ. La configuración se consume con `envFrom`.
- **Secret requerido**: `orderflow-orders-secrets` con las claves
  `orders-connection-string`, `rabbitmq-username`, `rabbitmq-password`.
  Su valor se suministra por un gestor de secretos del entorno; jamás se
  versionan las credenciales.
- **Imagen**: `ghcr.io/replace-owner/orderflow-orders-api:1.0.0` es un
  marcador, no una imagen publicada ni una URL comprobada. Sustituirla
  por una versión construida a partir del Dockerfile propio.

## Por qué tres probes diferentes

1. **startup** usa `/health/live` y tolera hasta dos minutos de arranque:
   la API prepara el esquema antes de empezar a servir solicitudes.
2. **liveness** usa `/health/live` y no depende de PostgreSQL o RabbitMQ;
   una caída transitoria de un servicio externo no debe reiniciar la API.
3. **readiness** usa `/health/ready`, que consulta PostgreSQL; si la API no
   puede atender solicitudes, Kubernetes la quita del balanceador.

## Por qué una sola réplica

Los despachadores Outbox actuales no incorporan *leasing* distribuido.
Con dos réplicas podrían publicar el mismo mensaje de forma concurrente:
la idempotencia del consumidor protege el negocio, pero implica duplicados
evitables. SignalR usa conexiones locales; para escalar horizontalmente
exigiría un mecanismo de coordinación/backplane o diseño de eventos de
actualización distribuido. Se mantiene una réplica hasta implementar esas
mejoras y validarlas.

## Cómo se desplegaría el sistema completo

1. Publicar las imágenes versionadas de Orders API, Inventory Worker y web
   en un registry compatible; configurar credenciales de extracción si son
   privadas.
2. Desplegar PostgreSQL con volúmenes persistentes, dos bases separadas,
   backups y permisos por servicio; asegurar que la inicialización se ejecute
   antes de habilitar el tráfico.
3. Desplegar RabbitMQ durable con persistencia y definición de topología.
4. Proveer `orderflow-orders-secrets` mediante gestor de secretos, no YAML
   versionado. El `ConfigMap` de Orders presupone DNS `rabbitmq`.
5. Desplegar Inventory Worker con su base, secretos y configuración, análogos
   a los de Orders. La ampliación de inventario también expone HTTP en el
   puerto interno 8081; si se incluye la consola de edición en Kubernetes,
   se requiere un Service interno adicional para ese endpoint y controles de
   autenticación/autorización. Estos recursos no forman parte del bonus de Orders.
6. Ajustar la imagen y aplicar los manifiestos de Orders API; después desplegar
   el frontend/Nginx con el proxy a Orders API y una ruta Ingress con TLS.
7. Comprobar probes, transiciones Pending/Confirmed/Rejected, idempotencia,
   reintentos y degradación/reconexión ante fallos. Solo entonces escalar.

La configuración de Compose sigue siendo el camino de ejecución obligatorio
para Q10. Esta guía es un diseño de despliegue, no un resultado verificado en
Kubernetes.

## Validación del YAML sin desplegar

```bash
kubectl kustomize k8s
```

El comando ensambla los manifiestos localmente. **No realiza** validación
contra un clúster o esquema del API server. Para un chequeo adicional se puede
usar un validador de manifiestos compatible con la versión objetivo del clúster,
o `kubectl apply --dry-run=server` cuando exista un clúster accesible.

## Documentación oficial

- https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/
- https://kubernetes.io/docs/concepts/configuration/manage-resources-containers/
- https://kubernetes.io/docs/concepts/configuration/configmap/
- https://kubernetes.io/docs/concepts/configuration/secret/
