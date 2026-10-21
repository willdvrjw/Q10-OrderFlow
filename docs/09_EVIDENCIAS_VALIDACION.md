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

## Validaciones HTTP 400 — comprobadas

**Fecha observada:** 21/10/2026 UTC (20/10/2026 en Colombia).

Se enviaron tres solicitudes `POST /orders` directamente al endpoint servido por Nginx. Todas devolvieron `HTTP/1.1 400 Bad Request` y `Content-Type: application/problem+json`, con errores específicos por campo:

| Entrada inválida | Campo | Respuesta |
|---|---|---|
| `clienteNombre: "   "` | `clienteNombre` | `Ingresa el nombre del cliente.` |
| `sku: "NO-EXISTE"` | `sku` | `Selecciona un SKU existente en el catálogo.` |
| `cantidad: 101` | `cantidad` | `La cantidad debe ser un número entero entre 1 y 100.` |

Esta evidencia confirma la validación y las respuestas HTTP; no constituye todavía una comprobación visual del formulario ni una consulta posterior que confirme ausencia de escrituras.

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

## Comprobación visual del formulario React

**Fecha observada:** 20/10/2026, hora local de Colombia.

Se abrió el panel OrderFlow en el puerto 8080 de Codespaces. Las capturas muestran cuatro pedidos cargados desde la API: tres confirmados y uno rechazado, con contador de pendientes igual a cero. El indicador de conexión muestra "Conectado en vivo", aunque su presencia por sí sola no prueba la entrega de un evento SignalR.

Se pulsó "Crear pedido" con cliente vacío, producto sin seleccionar y cantidad 1. La interfaz dibujó bordes rojos y mensajes específicos bajo `clienteNombre` y `sku`. El campo de cantidad válida no recibió error. El contador total se mantuvo en cuatro.

**Verificado:** validación visual de campos obligatorios y ausencia observable de un nuevo pedido en el panel. **Pendiente:** crear un pedido válido desde el formulario, observar refresco automático y ensayar respuesta a caída de SignalR.

## Formulario: creación real y actualización automática

**Fecha observada:** 20/10/2026, hora de Colombia.

Desde el panel web se creó el pedido `ABC6E820` (prefijo visible), cliente `Prueba Formulario Q10`, SKU `XYZ-02`, cantidad 1. El formulario mostró la notificación verde de registro inicial `Pending`. Sin recargar manualmente el navegador, la tabla mostró el pedido en estado `Confirmado`, y los indicadores pasaron de 4 a 5 pedidos totales y de 3 a 4 confirmados, con 1 rechazado y 0 pendientes.

**Alcance:** interfaz de creación y refresco sin recarga manual verificados. Aún no se ha atribuido causalmente el refresco a SignalR en vez del polling de respaldo. Falta revisar responsive y persistencia tras reinicio. El stock posterior de `XYZ-02` no se consultó en este paso.

## Verificación visual responsive (530 × 710)

**Fecha:** 20/10/2026, hora de Colombia. Se revisaron cuatro capturas de Chrome DevTools mostrando una ventana responsive de **530 × 710**, no 390 × 844. Se observan encabezado, banner, métricas en dos columnas, formulario y tarjetas de pedidos en una columna. Los estados y motivos de rechazo aparecen en líneas diferenciadas, después de los cambios de CSS. En las capturas no se aprecia desplazamiento horizontal, pero no se ha medido programáticamente el ancho de desbordamiento.


**Resolución aceptada por el responsable de la entrega:** se cierra la revisión visual con las capturas a 530 × 710. La comprobación adicional a 390 × 844 se omite por decisión expresa; no se presenta como una prueba realizada ni como incumplimiento demostrado. No se planifican más retoques visuales en esta fase.

Los mensajes rojos de la consola apuntan a la página de autenticación de `github.dev/pf-signin` y a un canal de mensajes de navegador/extensión; con estas capturas no se demuestra que se originen en el código React. Sigue **pendiente** comprobar el viewport 390 × 844 y confirmar el resultado en él.

## Persistencia de datos tras reinicio — verificada

Se ejecutó `docker compose restart` sobre los cinco contenedores, sin eliminar los volúmenes. Antes del reinicio se guardaron las respuestas de `GET /orders` y `SELECT sku, disponible FROM stock_items ORDER BY sku`, y después se compararon con los datos recuperados.

**Resultado observado:**
- Cinco pedidos antes y cinco después.
- La comparación por `id` de todos los objetos de pedidos devolvió `True`: datos idénticos.
- `diff -u` entre los archivos de inventario no produjo ninguna diferencia.
- API accesible tras el reinicio con la misma información.

Esto demuestra la persistencia bajo reinicio de contenedores en el Codespace actual; no equivale a una ejecución desde un repositorio clonado en otro host.

## Bonus Kubernetes — validación de ensamblado

En GitHub Codespaces se ejecutó `kubectl kustomize k8s > /tmp/orderflow-k8s.yaml` sin errores. La inspección `grep "^kind:" /tmp/orderflow-k8s.yaml` devolvió exactamente:

~~~text
kind: ConfigMap
kind: Service
kind: Deployment
~~~

**Resultado verificado:** los manifiestos se ensamblan mediante Kustomize y contienen los tres tipos de recursos exigidos. **Límite:** este comando no consulta un servidor de Kubernetes, no valida su admisión por el API server ni demuestra que los contenedores se desplieguen en un clúster. El enunciado no exige clúster operativo.

## SignalR — conexión WebSocket comprobada

En la consola de Chrome DevTools, bajo **Red → Socket**, se observó una solicitud `orders?id=...` correspondiente al Hub de Orders, con **estado HTTP 101** y **tipo websocket**. La duración se muestra como `Pendiente` porque la conexión permanece abierta.

**Aprobado:** negociación y establecimiento de WebSocket desde el frontend alojado en Codespaces.

**Comprobación ampliada:** posteriormente se recibió el frame `ordersChanged` con `type:1` y `orderId`, documentado en la sección de evento recibido. El polling de respaldo también se comprobó posteriormente al bloquear la conexión, según la sección siguiente.

## SignalR — evento de negocio recibido sobre WebSocket (aprobado)

**Fecha observada:** 20/10/2026, hora de Colombia.

En Chrome DevTools, Red → Socket → Mensajes, con una conexión WebSocket previamente negociada mediante HTTP **101**, se observó un frame de aplicación SignalR con `"type":1`, `"target":"ordersChanged"` y `"arguments":[{"orderId":"9a1feb0a-488b-4bed-af3b-e4099f9dbf81"}]`. El panel pasó a **6 pedidos totales, 5 confirmados, 1 rechazado y 0 pendientes**, en concordancia con la creación del pedido de comprobación.

Los frames de tipo `6` visibles en la misma conexión son mensajes de mantenimiento (*ping*) y no deben confundirse con el evento de aplicación.

**Resultado comprobado:** el Hub envió el evento `ordersChanged` y el navegador lo recibió realmente mediante WebSocket. **Límite:** la captura no aísla el efecto de ese frame sobre el refresco del panel, ya que también hay polling REST cada cinco segundos. El polling de respaldo se verificó en el ensayo descrito a continuación.

## Polling de respaldo con SignalR bloqueado — verificado

**Fecha observada:** 20/10/2026, hora de Colombia.

En Chrome DevTools → **Condiciones de la solicitud**, se activó el bloqueo por patrón `*://*/hubs/orders*`. La pantalla confirmó **«1 afectadas»** y el indicador de conexión de OrderFlow pasó a naranja. Mientras el bloqueo seguía habilitado se creó desde una terminal un pedido válido `Prueba Respaldo Polling`, SKU `ABC-01`, cantidad 1.

**Sin actualizar manualmente la página**, los indicadores del panel cambiaron de **6 a 7 pedidos totales** y de **5 a 6 confirmados**. Permanecieron en 0 pendientes y 1 rechazado. El bloqueo de SignalR seguía activo en la captura final.

**Conclusión verificada:** el panel continúa consultando Orders API mediante polling REST y recupera un estado nuevo aun cuando SignalR está bloqueado. La prueba no mide la latencia exacta de recuperación ni implica que el servicio WebSocket deba permanecer deshabilitado. Al terminar se debe desactivar el bloqueo de DevTools.

## Restauración de SignalR después del ensayo de bloqueo

**Fecha observada:** 20/10/2026, hora de Colombia.

Finalizado el ensayo de polling, se desactivó en DevTools la casilla **Habilitar el bloqueo y la limitación** y se recargó la aplicación. La captura muestra **0 solicitudes afectadas**, conexión WebSocket en estado **101** y el indicador superior de conexión en verde. El panel conserva **7 pedidos: 6 confirmados, 1 rechazado y 0 pendientes**.

La funcionalidad WebSocket quedó restaurada y el sistema regresó al estado de operación normal. No se han reinicializado ni eliminado los volúmenes de bases de datos durante esta comprobación.

## Pendiente de aprobación

- [x] Parar y recuperar Inventory Worker sin pérdida del pedido (ensayo REST + Compose verificado).
- [x] Parar y recuperar RabbitMQ sin pérdida del evento Outbox (pedido bca788f1 confirmado tras recuperación, stock XYZ-02 = 23).
- [x] Reiniciar contenedores con volúmenes existentes: cinco pedidos idénticos, sin cambios en inventario.
- [x] Verificar errores `400` desde la API (3 casos comprobados).
- [x] Verificar visualmente mensajes de error en el formulario (cliente y SKU obligatorios; captura del navegador).
- [x] Comprobar creación y actualización de pedidos desde el formulario web (pedido ABC6E820 confirmado).
- [ ] Verificar la versión responsive.
- [x] Establecer WebSocket y recibir `ordersChanged` por SignalR (HTTP 101 y frame `type:1`).
- [x] Comprobar polling de respaldo con SignalR bloqueado: contadores 6→7 y confirmados 5→6, sin recargar.
- [x] Ensamblar manifiestos Kubernetes con Kustomize; ConfigMap, Service y Deployment obtenidos (validación contra un clúster no realizada).
- [ ] Ejecutar el arranque obligatorio desde copia limpia en un host Docker sin ajustes especiales.
- [ ] Revisar la consistencia final entre README, código y guía de defensa.

Los elementos pendientes no deben presentarse como pruebas realizadas.
