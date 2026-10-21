# 09. Evidencias de validación — Q10-OrderFlow

Registro de resultados observados entre el 20 y el 21 de octubre de 2026, distinguiendo la hora local de Colombia y UTC. Las verificaciones se mantienen separadas de las características solamente implementadas.

## Compilación .NET 10

- `dotnet build OrderFlow.slnx`: cinco proyectos compilados sin errores.
- Revisión posterior de `RabbitMQ.Client 7.2.2`: se corrigió `DeliveryMode` para usar `DeliveryModes.Persistent`. La compilación posterior terminó satisfactoriamente.

## Dependencias reproducibles del frontend — verificadas

Se incorporó y versionó `web/package-lock.json` (npm lockfile v3) y el Dockerfile del frontend se actualizó para copiar `package.json` y `package-lock.json`, instalando paquetes con `npm ci --no-audit --no-fund`.

Desde la raíz del repositorio se ejecutó `docker compose up -d --build --no-deps web`; la construcción completó `npm ci` y `npm run build` sin errores (16,2 segundos), y el contenedor Nginx siguió activo. `curl -I --max-time 15 http://localhost:8080/` devolvió HTTP **200 OK**. `git status` mostró rama `main` sincronizada y árbol limpio.

**Alcance:** frontend compilado con el lockfile registrado; no implica que las etiquetas de imágenes base Docker estén fijadas por digest ni sustituye la prueba funcional backend ya documentada.

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

**Verificado:** validación visual de campos obligatorios y ausencia observable de un nuevo pedido en el panel. La creación de un pedido válido, el refresco automático y la caída controlada de SignalR se comprobaron posteriormente en los ensayos siguientes.

## Formulario: creación real y actualización automática

**Fecha observada:** 20/10/2026, hora de Colombia.

Desde el panel web se creó el pedido `ABC6E820` (prefijo visible), cliente `Prueba Formulario Q10`, SKU `XYZ-02`, cantidad 1. El formulario mostró la notificación verde de registro inicial `Pending`. Sin recargar manualmente el navegador, la tabla mostró el pedido en estado `Confirmado`, y los indicadores pasaron de 4 a 5 pedidos totales y de 3 a 4 confirmados, con 1 rechazado y 0 pendientes.

**Alcance:** interfaz de creación y refresco sin recarga manual verificados. Esta captura no aísla el mecanismo de actualización, pero ambos transportes se ensayaron por separado después. La vista responsive y la persistencia también se comprobaron en ensayos posteriores. El stock posterior de `XYZ-02` no se consultó en este paso.

## Verificación visual responsive (530 × 710)

**Fecha:** 20/10/2026, hora de Colombia. Se revisaron cuatro capturas de Chrome DevTools mostrando una ventana responsive de **530 × 710**, no 390 × 844. Se observan encabezado, banner, métricas en dos columnas, formulario y tarjetas de pedidos en una columna. Los estados y motivos de rechazo aparecen en líneas diferenciadas, después de los cambios de CSS. En las capturas no se aprecia desplazamiento horizontal, pero no se ha medido programáticamente el ancho de desbordamiento.


**Resolución aceptada por el responsable de la entrega:** se cierra la revisión visual con las capturas a 530 × 710. La comprobación adicional a 390 × 844 se omite por decisión expresa; no se presenta como una prueba realizada ni como incumplimiento demostrado. No se planifican más retoques visuales en esta fase.

Los mensajes rojos de la consola apuntan a `github.dev/pf-signin` y a un canal del navegador/extensión; no se demuestra que se originen en React. La comprobación de 390 × 844 **no se realizó por decisión expresa del candidato**, después de aceptar la revisión a 530 × 710.

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

## Auditoría desde una copia Git limpia — construcción y arranque

**Entorno:** el mismo GitHub Codespace, en el directorio temporal `/tmp/q10-orderflow-clean-audit`, separado de la carpeta original.

1. Se creó la copia con `git clone --branch main --single-branch . /tmp/q10-orderflow-clean-audit`; `docker compose config --quiet` no devolvió errores.
2. Tras detener los servicios originales sin eliminar sus volúmenes, se ejecutó `docker compose up -d --build` desde la copia.
3. Docker construyó con éxito las imágenes nuevas de Orders API, Inventory Worker y frontend (52,2 s de construcción reportada).
4. Se crearon la red `q10-orderflow-clean-audit_default` y volúmenes nuevos para PostgreSQL y RabbitMQ, independientes del proyecto original.
5. `docker compose ps` mostró los cinco servicios `Up`; PostgreSQL y RabbitMQ aparecieron `healthy`; el frontend publicó el puerto `8080`.

**Estado ampliado:** los contenedores del clon arrancaron, y posteriormente se comprobaron la API, el seed, la lista vacía y un pedido confirmado. Ver «Recorrido completo desde la copia limpia». El ensayo no demuestra todavía portabilidad en otro host ni elimina la particularidad de `iptables-legacy` observada anteriormente en este Codespace.

### Recuperación de la conectividad en el clon

En la red nueva de la copia limpia, la conexión TCP inicial entre contenedores expiraba por la política heredada `iptables-legacy FORWARD DROP`, tal como había sucedido antes en el Codespace. Se identificó el puente `br-70dcb87d5e79`, se añadió una regla temporal exclusivamente para comunicaciones dentro de ese puente y un contenedor separado de PostgreSQL respondió `SELECT 1`.

Tras `docker compose restart orders-api inventory-worker`, `docker compose ps` mostró los cinco servicios activos y PostgreSQL/RabbitMQ saludables. La petición `curl -i --max-time 15 http://localhost:8080/health/ready` respondió **HTTP 200** con `{"status":"ready"}`.

**Alcance:** API lista en el clon. El catálogo y el pedido se comprobaron posteriormente, como consta en el recorrido completo documentado. El ajuste temporal de reglas del host no se incluyó en el repositorio y no demuestra, por sí solo, despliegue en cualquier máquina Docker.


### Recorrido completo desde la copia limpia — aprobado

**Fecha observada:** 21/10/2026 UTC (20/10/2026 en Colombia).

Una vez restablecida la comunicación entre contenedores del Codespace, se comprobó mediante el mismo puerto 8080 de la copia independiente:

| Comprobación en copia limpia | Resultado |
|---|---|
| `GET /health/ready` | HTTP 200, `{"status":"ready"}` |
| `GET /products` | HTTP 200; SKU `ABC-01`, `XYZ-02`, `TEST-03` |
| `GET /orders` inicial | HTTP 200, lista vacía `[]` |
| `POST /orders`, `ABC-01`, cantidad 1 | HTTP 201 y `Pending` |
| `GET /orders` posterior | Mismo pedido en `Confirmed` |
| Stock de la copia independiente | `ABC-01=99`, `TEST-03=3`, `XYZ-02=25` |

El pedido de prueba fue `69e7df92-5e33-46ef-93de-2073c208fbd1`; se creó a las 02:27:30 UTC y se confirmó a las 02:27:31 UTC (aprox. 1,10 s después). Estos valores corresponden **solo** a la copia limpia, sin modificar la instalación original de siete pedidos.

**Conclusión:** compilación, inicialización de datos, API, mensajería y reserva quedaron verificadas desde una copia nueva con red y volúmenes propios dentro de este Codespace. No se ha demostrado portabilidad a otro host Docker. El firewall de este Codespace necesitó una regla temporal de puente; no se debe presentar esa corrección como un requisito de instalación normal.

## Verificación de cierre y repositorio público

**Ejecución final observada:** 20/10/2026 (hora Colombia), correspondiente al 21/10/2026 UTC.

- `docker compose --profile tests run --build --rm backend-tests`: compiló los cinco proyectos .NET 10 y ejecutó xUnit con **5 pruebas aprobadas, 0 fallidas y 0 omitidas**; tiempo de pruebas ~14,8 s, duración total de construcción ~30,5 s. Casos de dominio: validación, confirmación única y rechazo terminal; casos PostgreSQL: idempotencia por pedido y reserva concurrente sin stock negativo.
- `docker compose exec -T web wget -qO- http://orders-api:8080/orders`: Nginx se comunicó con Orders API por la red Docker interna; la respuesta JSON incluyó **7 pedidos**, conservados de la instalación original.
- `git status --short`: no devolvió cambios locales; árbol de trabajo limpio después de sincronizar la documentación y el código.
- Consulta del repositorio mediante GitHub: `willdvrjw/Q10-OrderFlow`, rama `main`, **visibilidad `public`**. No se cambió la visibilidad de otros repositorios.

**Cierre:** código, tests, README y guía de defensa se contrastaron con los archivos actuales. El proyecto es entregable como ejercicio técnico con evidencia de compilación y funcionamiento. Los límites restantes son validación en un host Docker independiente, inyección exacta de caída entre COMMIT y ACK, y despliegue Kubernetes real (no exigido por el bonus).

## Ampliación de inventario — ingreso de unidades desde la interfaz

**Observado en capturas compartidas el 21/10/2026, hora de Colombia.** En la rama `feat/inventory-and-ui-refinement` se registró mediante el formulario de inventario un ingreso de 3 unidades de `TEST-03`:

- Existencias de Agenda de escritorio (`TEST-03`): **3 → 6**; el indicador cambió de **Existencias bajas** a **Disponible**.
- La interfaz mostró: «Existencias actualizadas: 6 unidades disponibles de TEST-03».
- El panel continuó mostrando **7 pedidos, 6 confirmados, 1 rechazado, 0 pendientes**. No se alteraron los estados de los pedidos ya terminados.
- El indicador de inventario mostró «En tiempo real», señal de una conexión activa al canal SignalR.

**Alcance del primer ensayo:** las capturas de ingreso muestran la actualización local y su confirmación. El transporte WebSocket se comprobó posteriormente, según se documenta en la sección siguiente. Todavía falta probar el polling de respaldo propio del inventario y una reserva nueva que descuente existencias.

## Comprobación del canal de inventario en dos pestañas

**Fecha:** 21/10/2026 (hora de Colombia). En Chrome DevTools, la captura mostró las conexiones `inventory?id=...` y `orders?id=...` establecidas con **HTTP 101**. Se seleccionó `inventory?id=...` y la pestaña **Mensajes**, donde inicialmente aparecían los ping `{"type":6}` de SignalR.

Después se indicó modificar desde otra pestaña las existencias de `TEST-03` mediante **Establecer cantidad total** a 3 unidades, manteniendo DevTools abierto en la primera. **El usuario confirmó expresamente «Sí apareció»** cuando se le preguntó por el mensaje `inventoryChanged`. Esta es una confirmación textual del resultado, no una captura del frame completo: no se verificó visualmente aquí el contenido exacto de sus argumentos ni se mostró el valor final de stock tras el ajuste.

**Conclusión acotada:** conexión WebSocket de inventario observada y recepción del evento de ajuste confirmada por el usuario. Pendiente comprobar un evento producido por **reserva**, además de fallos controlados de SignalR y recuperación mediante polling propio del inventario.

## Hallazgo de la repetición de pedidos rechazados — corrección pendiente de revalidación

**Evidencia de las capturas del 21/10/2026:** después de utilizar la opción `Crear nuevo pedido` sobre `Prueba Stock Insuficiente`, el panel mostró **8 pedidos, 6 confirmados y 2 rechazados**, incluido un nuevo rechazado `7E9B5AD9` para `TEST-03` × 4 mientras el inventario seguía sin alcanzar. El formulario había precargado los datos del rechazado y mostrado que se crearía un pedido distinto. Esto evidenció que el flujo auxiliar de repetición no comprobaba existencias; no es un defecto de la máquina de estados original.

**Corrección implementada en la rama, aún no probada en Codespaces:** el botón de repetición queda deshabilitado sin saldo suficiente y presenta cantidades disponibles; la lista recibe los saldos actualizados por la vista de Inventory. Al prepararlo se realiza `GET /inventory` fresco y, antes del POST desde un formulario originado en un rechazado, se repite la comprobación con `cache: no-store`. El resto de creación de pedidos mantiene las reglas de Q10. No se elimina ni modifica ninguno de los 8 pedidos registrados.

**Verificaciones por realizar:** reconstruir frontend, comprobar que no se crea un noveno pedido con stock insuficiente, comprobar habilitación tras reposición y que las cinco pruebas backend siguen pasando. La consulta preventiva no reserva stock y no sustituye la decisión transaccional del Worker ante concurrencia.

## Revalidación — prevención de duplicados al repetir rechazados con cantidad superior al stock

**Evidencia visual del 21/10/2026:** con el formulario preparado a partir de `Prueba Stock Insuficiente`, se editó manualmente la cantidad solicitada de **4 a 5** para `TEST-03` mientras había **4 unidades** de existencias. El formulario mostró `Solo hay 4 unidades disponibles y se solicitaron 5.` y `No se creó un pedido nuevo. Repón las existencias y vuelve a verificar antes de guardar.`

El panel permaneció en **8 pedidos**, con **6 Confirmed**, **2 Rejected** y **0 Pending**. Se observó así que la validación preventiva justo antes del envío impide una repetición innecesaria cuando la cantidad se incrementa después de precargar el pedido rechazado. Esta evidencia **no prueba aún** el camino de creación exitosa con cuatro unidades, ni garantiza que no haya una reserva concurrente entre la consulta y el procesamiento asíncrono.

## Segundo hallazgo de repetición — variación de cantidad con stock suficiente

**Observación comunicada por el usuario (21/10/2026):** después de reponer inventario, se volvió a abrir un pedido previamente rechazado por **4 unidades**; se cambió el campo a **5**, y se creó un pedido nuevo independiente porque la versión anterior comprobaba únicamente la disponibilidad, pero no la equivalencia con la solicitud original. No se dispone aquí del ID ni del estado final de ese último pedido, y no se alterará retroactivamente el historial.

**Corrección incorporada en la rama, pendiente de validación funcional en navegador:** en modo repetición se mantienen fijos cliente y SKU; se detecta de inmediato cualquier cantidad diferente y se inhabilita `Confirmar repetición`. La validación se repite dentro del manejador `submit`; además se consulta el pedido original mediante `GET /orders/{id}` y se compara su cliente, SKU, cantidad y estado terminal antes del POST, junto con una nueva consulta de stock. Se añadió `Cancelar repetición` para volver al modo de registro normal.

**Invariantes:** los estados `Pending`, `Confirmed` y `Rejected` siguen sin modificaciones; ningún pedido rechazado se reabre. Una nueva solicitud con exactamente los mismos datos seguirá recibiendo su propio ID, y su confirmación dependerá del Worker. Pendiente reconstruir el frontend y comprobar cantidad distinta con saldo suficiente sin crear otro pedido.

## Validación de concurrencia optimista en ajustes — HTTP 409

**Observado el 21/10/2026, 06:27 UTC, en Codespaces:** se consultó `GET /inventory` y se obtuvo `ABC-01=96`, `TEST-03=6`, `XYZ-02=20`. A continuación se envió `POST /inventory/adjustments` con `sku=TEST-03`, `operacion=establecer`, `cantidad=1` y `existenciaEsperada=999999` (valor deliberadamente incorrecto).

**Resultado:** Nginx devolvió **HTTP 409 Conflict** con `{"mensaje":"Las existencias cambiaron. Actualiza y vuelve a intentar.","disponibleActual":6}`. La nueva consulta `GET /inventory` devolvió **exactamente las mismas tres existencias**: 96, 6 y 20.

**Conclusión:** el ajuste basado en un saldo obsoleto no sobrescribe ni modifica el inventario. No se crearon pedidos; este ensayo no equivale a probar un conflicto entre dos peticiones simultáneas, que se cubre parcialmente mediante el test de PostgreSQL.

## Notificaciones y mensajes de validación — verificación visual

**Fecha:** 21/10/2026 (hora local de Colombia). Tras sustituir los avisos del frontend por el componente `FlashNotice`, se solicitó al usuario comprobar manualmente dos acciones: cerrar un aviso mediante el botón **X** y verificar que el error rojo de cantidad distinta al repetir un rechazado apareciera centrado.

**Resultado comunicado:** el usuario confirmó expresamente «Sí, funcionan correctamente». El componente permite cerrar las notificaciones de éxito, error e información sin modificar pedidos, inventario ni la validación de campos; los errores de campo siguen visibles mientras el dato sea inválido, incluso cuando se cierra un aviso.

**Alcance:** aprobación visual confirmada por el usuario; todavía falta ejecutar nuevamente la compilación del frontend desde el último commit y las cinco pruebas finales antes de fusionar la rama.

## Ejecución final previa a integración — 21/10/2026

Desde la rama `feat/inventory-and-ui-refinement`, el candidato ejecutó `git pull --ff-only origin feat/inventory-and-ui-refinement`, el perfil de pruebas backend y la reconstrucción del frontend, seguido de consultas HTTP y `git status --short`. Se observaron los siguientes resultados:

| Control | Resultado real |
|---|---|
| `docker compose --profile tests run --build --rm backend-tests` | **5 tests aprobados, 0 fallidos, 0 omitidos**; compilación de los cinco proyectos .NET 10 correcta |
| `docker compose up -d --build --no-deps web` | Imagen web construida, contenedor activo; se reutilizó la caché del Dockerfile/NPM, no se solicitó reconstrucción `--no-cache` |
| `GET /health/ready` | `{"status":"ready"}` |
| `GET /inventory` | `ABC-01=96`, `TEST-03=6`, `XYZ-02=20` |
| `GET /orders` | **10 pedidos:** 8 `Confirmed`, 2 `Rejected`, 0 `Pending` |
| `git status --short` | Sin modificaciones locales pendientes |

Los diez pedidos reflejan la actividad acumulada de las pruebas manuales; **estos comandos de validación no crearon pedidos**. El registro no prueba ejecución en otro host ni todos los escenarios opcionales: quedan por ensayar la caída del WebSocket de inventario y el recorrido completo de una repetición exacta tras la última corrección de la UI.

## Pendiente de aprobación

- [x] Parar y recuperar Inventory Worker sin pérdida del pedido (ensayo REST + Compose verificado).
- [x] Parar y recuperar RabbitMQ sin pérdida del evento Outbox (pedido bca788f1 confirmado tras recuperación, stock XYZ-02 = 23).
- [x] Reiniciar contenedores con volúmenes existentes: cinco pedidos idénticos, sin cambios en inventario.
- [x] Verificar errores `400` desde la API (3 casos comprobados).
- [x] Verificar visualmente mensajes de error en el formulario (cliente y SKU obligatorios; captura del navegador).
- [x] Comprobar creación y actualización de pedidos desde el formulario web (pedido ABC6E820 confirmado).
- [x] Revisar responsive a 530 × 710 y aplicar mejoras CSS; comprobación adicional a 390 × 844 omitida expresamente.
- [x] Establecer WebSocket y recibir `ordersChanged` por SignalR (HTTP 101 y frame `type:1`).
- [x] Comprobar polling de respaldo con SignalR bloqueado: contadores 6→7 y confirmados 5→6, sin recargar.
- [x] Ensamblar manifiestos Kubernetes con Kustomize; ConfigMap, Service y Deployment obtenidos (validación contra un clúster no realizada).
- [x] Ejecutar y comprobar funcionalmente una copia limpia con volúmenes independientes en el mismo Codespace (requiere ajuste temporal del firewall del host).
- [ ] Comprobar despliegue en un host Docker diferente sin ajustes especiales; el comando literal `docker compose up` en primer plano no fue el utilizado en este ensayo (se usó `docker compose up -d --build`).
- [x] Revisar la consistencia final entre README, código y guía de defensa; corregidos nombres y rutas obsoletas.

Los elementos pendientes no deben presentarse como pruebas realizadas.
