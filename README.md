# Q10-OrderFlow

Evaluación Fullstack: registro de pedidos, reserva asíncrona de inventario y panel de operaciones.

**Estado del proyecto:** solución de pedidos, inventario y alta dinámica de productos integrada en `main` y validada en GitHub Codespaces. Siete pruebas backend aprobadas y funcionamiento del alta, la reserva y el ajuste de existencias comprobado por el candidato.

- **Backend:** Orders API e Inventory Worker en .NET 10, RabbitMQ y PostgreSQL; pedidos `Pending`, `Confirmed` y `Rejected`.
- **Robustez:** Outbox transaccional, reintentos, idempotencia por pedido y descuento atómico; recuperación verificada ante detenciones de RabbitMQ y Worker.
- **Pruebas:** **siete pruebas backend aprobadas**: tres de dominio, dos de inventario y concurrencia sobre PostgreSQL y dos de creación dinámica/idempotencia de productos sobre PostgreSQL.
- **Frontend:** React + TypeScript con validación, filtrado, SignalR y recuperación por polling. Dependencias fijadas en `web/package-lock.json` e instaladas con `npm ci`.
- **Despliegue:** compilación, seed y creación/reserva comprobados en un clon independiente dentro del mismo Codespace; bonus Kubernetes ensamblado sintácticamente, no desplegado en un clúster.

Los resultados y límites de verificación están documentados en [evidencias de validación](docs/09_EVIDENCIAS_VALIDACION.md) y en la [auditoría QA final según la rúbrica de Q10](docs/10_AUDITORIA_QA_ENTREGA.md).

## Inicio rápido — la aplicación real mediante Docker Compose

**Código público y entrega:** https://github.com/willdvrjw/Q10-OrderFlow

**Interfaz de la aplicación:** http://localhost:8080 (después del arranque con Docker).

> Esta prueba se entrega como **repositorio reproducible con Docker Compose**, no como un sitio web hospedado. El evaluador ejecuta los cinco servicios en su propio equipo y puede volver a abrir la interfaz mientras Docker permanezca activo. No es necesario compartir enlaces temporales ni instalar .NET, Node.js, PostgreSQL o RabbitMQ por separado.

### Requisitos (Windows, macOS y Linux)

- **Docker Engine activo con Docker Compose v2**: [Docker Desktop](https://docs.docker.com/desktop/) en Windows/macOS o Docker Engine con complemento Compose en Linux. En Windows se utilizan **contenedores Linux**.
- Git es opcional: también se puede descargar el repositorio como ZIP y descomprimirlo.
- Conexión a Internet durante la primera descarga y compilación de las imágenes; espacio y memoria suficientes para los cinco servicios.
- Navegador moderno con JavaScript habilitado (Chrome, Edge, Firefox o Safari). La validación práctica documentada se realizó en Chrome/Chromium; no se certificó cada navegador y sistema operativo.

### Tres pasos para ejecutar

1. Descarga el repositorio desde GitHub (**Code → Download ZIP**) y descomprímelo. Si tienes Git, usa estos comandos:

   ~~~sh
   git clone https://github.com/willdvrjw/Q10-OrderFlow.git
   cd Q10-OrderFlow
   ~~~

   Con ZIP, abre una terminal en la carpeta descomprimida que contiene `compose.yaml`.

2. Inicia Docker Desktop (o Docker Engine) y ejecuta **un único comando**:

   ~~~sh
   docker compose up
   ~~~

   Docker Compose descarga las dependencias, compila Orders API, Inventory Worker y React, inicia PostgreSQL y RabbitMQ, y carga automáticamente los productos iniciales. El primer arranque puede tardar varios minutos según la conexión y el hardware.

3. Abre **http://localhost:8080** en el navegador. Para confirmar que la aplicación está disponible, consulta **http://localhost:8080/health/ready**: debe responder con `{"status":"ready"}`. Crea un pedido en el panel; se registra inicialmente como `Pending` y termina en `Confirmed` o `Rejected` tras la reserva asíncrona.

**Para volver a abrirla:** mientras Docker siga ejecutándose, visita de nuevo `http://localhost:8080`. Si detuviste los servicios, vuelve a ejecutar `docker compose up` desde la misma carpeta. Los volúmenes conservan pedidos, productos y existencias.

**Seguridad de la demostración:** no incluye autenticación de operaciones administrativas porque Q10 no la exige. El puerto web se publica solo en `127.0.0.1` (la máquina local); no abras el servicio a Internet ni introduzcas datos personales reales. Un despliegue público permanente no forma parte de esta entrega.

### Operación diaria y pruebas

| Acción | Comando |
|---|---|
| Arranque obligatorio de Q10 | docker compose up |
| Iniciar en segundo plano | docker compose up -d --build |
| Ver servicios y estado de salud | docker compose ps |
| Últimos registros | docker compose logs --tail=80 |
| Ejecutar las **7 pruebas** en un comando | docker compose --profile tests run --build --rm backend-tests |
| Detener sin borrar información | docker compose down |
| Volver a iniciar sin eliminar datos | docker compose up -d |

PostgreSQL y RabbitMQ utilizan volúmenes persistentes. **Nunca ejecutes docker compose down -v si deseas conservar los pedidos, productos e inventario.** Las pruebas crean bases temporales separadas de las bases de demostración.

### Problemas comunes

- **Docker no está instalado o no está listo:** comprueba docker compose version y espera a que Docker Desktop indique que el motor está encendido.
- **Puerto 8080 ocupado:** crea un archivo .env en la raíz con la línea ORDERFLOW_WEB_PORT=8090; inicia Compose de nuevo y abre http://localhost:8090. No cambies los puertos internos de los servicios.
- **Error 502 al principio:** Orders e Inventory pueden seguir inicializando PostgreSQL; espera y revisa docker compose ps y docker compose logs --tail=80 web orders-api inventory-worker postgres. El proxy Nginx utiliza DNS dinámico para tolerar contenedores recreados.
- **Pedido aún en Pending:** consulta docker compose ps y los registros de rabbitmq e inventory-worker. Si el broker o Inventory no responde, el evento queda pendiente y el procesamiento se recupera al regresar.
- **El inventario no vuelve al valor inicial:** correcto; el volumen conserva consumos anteriores y nuevos SKU. En un primer arranque limpio la semilla carga tres productos.
- **El equipo utiliza Docker Desktop pero no puede comunicarse con los servicios:** comprueba `docker compose ps` y los logs. El Codespace utilizado para las pruebas tuvo una incidencia particular de firewall que no debe asumirse como requisito general para Windows, macOS o Linux.
- **Cambiaste contraseñas después de iniciar PostgreSQL:** el volumen conserva las credenciales previas. No cambies contraseñas en una base ya inicializada sin realizar la rotación correspondiente; los valores por defecto son solo para demo local.

No se exige archivo .env para empezar. El ejemplo .env.example enumera parámetros opcionales (también el puerto). Las contraseñas de demostración **no** están diseñadas para producción y no debes publicar un .env con secretos.

**Compatibilidad esperada:** la interfaz es una aplicación web React/HTML/CSS y utiliza SignalR con polling de respaldo; funciona en navegadores modernos con JavaScript. Se probó manualmente con Chrome/Chromium en Codespaces. No se realizaron pruebas independientes en cada versión de Windows, macOS, Linux, Edge, Firefox o Safari; evita interpretar compatibilidad por estándares como una certificación universal.

---

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

## Alta de productos nuevos (ampliación adicional)

En **Inventario disponible** se incorpora el botón **Agregar producto nuevo**, bajo las tarjetas de existencias. Solicita **nombre**, **SKU único** y **existencias iniciales** (admite 0). Después de guardar, el producto aparece tanto en las tarjetas y el actualizador de inventario como en el selector **Nuevo pedido**, sin editar la semilla ni reiniciar el sistema.

- `POST /products` en Orders API registra `{"sku":"NUEVO-04","nombre":"Producto de ejemplo","disponible":8}`. Valida nombre (1–160), SKU normalizado en mayúsculas (1–64 letras, números, `.`, `-`, `_`) y existencias (0–1.000.000). Responde **201** solo cuando se ha preparado el SKU en Inventory y guardado su catálogo persistente en Orders; **400** si faltan datos, **409** si el SKU está repetido y **503** si Inventory no está disponible.
- `GET /products` une los tres productos iniciales con los productos creados y persistidos en Orders. `GET /products/{sku}` consulta un producto. El formulario de pedidos usa la misma validación Q10 con este catálogo ampliado.
- Orders coordina el alta mediante HTTP interno hacia `POST /inventory/products`. Inventory **crea registro idempotente y stock inicial en una transacción local**, sin leer ni modificar la base de Orders. Un reintento idéntico no restablece existencias ni crea duplicados; un SKU ya existente con otros datos produce **409**. Los pedidos continúan procesándose **asíncronamente por RabbitMQ**, sin cambiar sus estados ni reservas.
- Cada servicio incorpora su propia tabla nueva mediante DDL `CREATE TABLE IF NOT EXISTS` después de `EnsureCreated`, también con volúmenes previamente inicializados; **no borra ni reinicia los pedidos/stock existentes**. Si Inventory confirmó y Orders no alcanzó a guardar, se puede **reintentar la misma solicitud** para completar el alta sin sobrescribir stock. No hay transacción distribuida entre servicios: es un compromiso explícito de esta extensión.
- La lista de tarjetas tiene **scroll independiente**, se mantiene a la altura del formulario **Actualizar existencias**, y el botón de alta permanece accesible bajo las tarjetas. En otro navegador, una notificación `inventoryChanged` desencadena actualización de existencias y sincronización del catálogo; el polling sigue disponible.
- La entrada de búsqueda de pedidos y los campos del inventario ahora tienen `id` y `name` explícitos para evitar la incidencia de autocompletado señalada por Chrome DevTools. Los errores sobre `chrome.runtime` o canales de mensajes de extensiones se deben investigar en el navegador: no proceden necesariamente de la aplicación.
- **Seguridad de demostración:** igual que el ajuste de inventario original, las rutas administrativas no incorporan autenticación. Antes de uso público real se requieren roles, permisos y auditoría; esta versión está pensada para la prueba técnica.

Ejemplo sin cambiar datos existentes:

~~~bash
curl -i -X POST http://localhost:8080/products \
  -H "Content-Type: application/json" \
  -d '{"sku":"DEMO-04","nombre":"Producto demostración","disponible":8}'
curl -fsS http://localhost:8080/products
curl -fsS http://localhost:8080/inventory
~~~

**Validación de esta ampliación:** Orders API, Inventory Worker y React/TypeScript compilaron con Docker; **7/7 pruebas backend aprobadas**. El usuario verificó alta de producto, aparición en pedidos e inventario, ajuste y reserva, bloqueo de SKU duplicados y diseño de escritorio/móvil. Se comprobó adicionalmente **HTTP 409** al duplicar `FRP`, comparando catálogo, inventario e historial antes/después sin cambios, y se verificó persistencia idéntica de los tres recursos tras reiniciar los servicios. Nginx pasó a resolver dinámicamente upstreams con Docker DNS y devolvió HTTP 200 en las rutas verificadas. Queda sin comprobación independiente la incidencia asíncrona de extensiones de Chrome, SignalR posterior al cambio de proxy, una prueba de estrés con muchos SKU y el despliegue en otro host Docker.

## Gestión de existencias (ampliación de la prueba)

La página incorpora una sección **Inventario disponible**, con consulta del stock actual, selección de SKU y dos operaciones: **Agregar unidades** y **Establecer cantidad total**. Inventory es el único dueño de la tabla `stock_items`; Orders API nunca actualiza su base.

- `GET /inventory`: consulta existencias actuales desde Inventory Worker (HTTP interno).
- `POST /inventory/adjustments`: envía `sku`, `operacion` (`agregar` o `establecer`), `cantidad` y `existenciaEsperada`.
- El ajuste se realiza mediante una sola sentencia SQL condicional: si las existencias cambiaron desde que el operador consultó, devuelve HTTP **409 Conflict** y no sobrescribe la reserva concurrente.
- La operación de agregar requiere al menos una unidad; establecer permite cero. Inventory emite `inventoryChanged` **después de confirmar cada reserva o ajuste**; el navegador consulta las existencias de inmediato y mantiene consultas cada diez segundos como respaldo. La imagen de portada se encuentra en `web/public/images/centro-logistico-orderflow.webp`.
- Los indicadores de inventario **Disponible**, **Existencias bajas** (de 1 a 5) y **Agotado** son visuales; no introducen estados adicionales en los pedidos.
- Los pedidos conservan exactamente `Pending`, `Confirmed` y `Rejected`. Reponer existencias beneficia a nuevas reservas y a pedidos `Pending` **que aún no han sido procesados**. Los pedidos ya `Rejected` permanecen rechazados. En cada rechazado, **«Crear nuevo pedido» se habilita únicamente cuando el inventario consultado contiene unidades suficientes**; mientras falten, indica la cantidad disponible y enlaza a Inventario. Al pulsarlo consulta nuevamente el stock antes de precargar el formulario, y al guardar realiza una segunda consulta actualizada (`cache: no-store`). Nunca envía automáticamente ni modifica el pedido anterior.
- **Repetición exacta de un rechazado:** el formulario muestra el ID de origen, conserva el mismo cliente y SKU sin permitir su edición y exige **exactamente la cantidad registrada**. Si se cambia de 4 a 5 unidades, indica la diferencia al instante y bloquea «Confirmar repetición», aunque existan 6 o más unidades en inventario. Antes de enviar vuelve a leer `GET /orders/{id}` y `GET /inventory` para verificar el rechazo original, los datos y la disponibilidad. «Cancelar repetición» restaura el registro manual para pedidos con otros datos. Una repetición válida genera un nuevo identificador porque los pedidos `Rejected` son terminales; el pedido histórico no se reabre.
- La lista distingue **estado histórico de pedido** de **stock disponible hoy**: cuando una reposición ya alcanza la cantidad del rechazado, reemplaza el texto visible del motivo anterior por «Inventario repuesto · N unidades disponibles», en verde. El pedido sigue `Rejected`; su motivo original se conserva en la base y en la ayuda contextual de la fila.
- Se conserva el historial de pedidos y el descuento transaccional. La notificación de inventario no participa en la confirmación de la reserva.
- **Límite de concurrencia:** la consulta preventiva no bloquea unidades. Si otra reserva utiliza el stock entre la última comprobación y el procesamiento del pedido, Inventory puede devolver `StockRejected` legítimamente; prohibir esto requeriría modificar la reserva asíncrona exigida por la prueba. Esta protección de interfaz **solo afecta la repetición de rechazados**; `POST /orders` y el formulario manual conservan la semántica original de aceptar pedidos válidos en `Pending`, aunque después se rechacen por insuficiencia.

**Validación anterior de la interfaz e inventario:** cinco pruebas aprobadas en la etapa inicial (ahora ampliadas a siete); la recepción de `inventoryChanged` se observó con Chrome antes del ajuste de DNS dinámico de Nginx. Los pedidos `Rejected` siguen siendo terminales y las reglas originales se conservan. Permanecen sin ensayo específico el polling de inventario con WebSocket bloqueado y una repetición exacta de rechazado de extremo a extremo. Véase `docs/09_EVIDENCIAS_VALIDACION.md`.

## Siete tests backend, un comando

~~~bash
docker compose --profile tests run --build --rm backend-tests
~~~

Este comando utiliza el perfil opcional de pruebas y no altera el arranque normal con docker compose up. Ejecuta tres pruebas de dominio y cuatro de integración sobre PostgreSQL real:

1. Validación de nombre, SKU y cantidad.
2. Pedido creado en Pending, con transición única a Confirmed.
3. Pedido rechazado como estado terminal.
4. Idempotencia: reenviar OrderCreated con el mismo OrderId y EventId distinto no descuenta inventario dos veces.
5. Concurrencia: dos reservas distintas sobre el mismo SKU no generan stock negativo y solo una puede descontar cuatro de cinco unidades.
6. Alta de un SKU nuevo, reserva real y reintento idempotente que no restaura unidades.
7. Alta duplicada que no puede sobrescribir existencias previamente registradas.

**Aislamiento:** las cuatro integraciones PostgreSQL crean una base temporal distinta por test y la eliminan al finalizar; no modifican los datos del panel. Requieren permisos para crear y eliminar bases en PostgreSQL de pruebas. El perfil Docker configura la conexión automáticamente. **Resultado observado en Codespaces el 21/10/2026: siete aprobadas, ninguna fallida.**

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
- Los tres productos de la semilla son fijos, pero el catálogo admite altas persistentes. La coordinación HTTP interna de un alta no es una transacción distribuida: el alta de existencias es idempotente y permite reintentar una operación parcialmente completada.
- Las variables de entorno incluyen credenciales de prueba por defecto para que docker compose up funcione sin configuración manual. No reutilizarlas en producción. El archivo .env local no se versiona.

## Qué haría distinto con más tiempo

Reemplazar inicialización de esquemas por migraciones; introducir privilegios mínimos y gestión de secretos; ampliar métricas, trazas, alarmas, políticas de DLQ y pruebas de fallos; administrar productos mediante un catálogo independiente y agregar controles de seguridad adecuados a producción.

## Organización y defensa técnica

- src/Orders.Api: dominio, validaciones, API, outbox, consumidor y SignalR.
- src/Inventory.Worker: stock, transacciones, idempotencia y consumo de eventos.
- src/OrderFlow.Contracts: contratos de eventos.
- src/OrderFlow.Messaging: transporte y topología RabbitMQ.
- tests/OrderFlow.Tests: siete pruebas backend, cuatro con PostgreSQL real.
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
- docs/10_AUDITORIA_QA_ENTREGA.md: matriz ponderada, trazabilidad y límites honestos de compatibilidad.

**Alcance de validación:** el sistema se ejecutó desde una copia nueva dentro de GitHub Codespaces, con volúmenes independientes. El firewall específico de ese entorno requirió un ajuste local de conectividad entre contenedores. No se ha probado todavía en un host Docker diferente. La emulación responsive original se verificó a 530 × 710. Para la ampliación del inventario el usuario confirmó visualmente la vista móvil a 390 px sin aportar nueva captura; no equivale a un ensayo independiente de estrés con muchos SKU. Kubernetes se verificó mediante Kustomize, sin clúster. Ver `docs/09_EVIDENCIAS_VALIDACION.md`.


## QA reproducible de Docker y Codespaces — actualización de octubre de 2026

La interfaz React cargando **no basta para declarar la aplicación saludable**:
pedidos, catálogo e inventario deben responder desde los dos backends. Compose
incluye healthchecks reales para PostgreSQL (dos bases), RabbitMQ, Orders API,
Inventory Worker y el proxy web. No se corrigen firewalls del host mediante
scripts del proyecto ni se exponen las API de administración a Internet.

**Arranque recomendado desde un clon limpio:**

~~~bash
docker compose config --quiet
docker compose up -d --build --wait --wait-timeout 240
~~~

La instrucción obligatoria de Q10, docker compose up, continúa siendo válida.
La opción --wait espera que los servicios estén saludables y falla si no lo
consiguen; es la forma recomendada de confirmar el estado antes de mostrar
la demo. Consultar http://127.0.0.1:8080/health/ready y
http://127.0.0.1:8080/inventory/health/ready; ambas deben devolver
{"status":"ready"}.

**Control automático independiente:** .github/workflows/docker-qa.yml prueba
en GitHub Actions un checkout nuevo, la compilación con Docker Compose, una
operación completa de producto y pedido, el bloqueo del SKU duplicado (409),
la suite de siete tests backend y la persistencia tras reiniciar. Consultar
la pestaña **Actions** antes de afirmar que una versión concreta pasó. Una
prueba fallida bloquea la evidencia de entrega; no sustituirla por capturas.

**Codespaces:** .devcontainer/devcontainer.json configura un Docker Engine
aislado mediante la característica oficial Docker-in-Docker. Un nuevo Codespace
inicia los servicios automáticamente con scripts/codespaces-start.sh y
ofrece el puerto 8080 en una vista previa **privada**. No es alojamiento
permanente: el Codespace se detiene con la sesión o por inactividad. Una
instancia anterior necesita una reconstrucción para adoptar este entorno;
**no reconstruyas un Codespace con volúmenes históricos importantes sin
respaldar primero las bases y colas**, pues cambiar de Docker Engine puede
hacer que los volúmenes anteriores no aparezcan en el nuevo entorno.
Lo más seguro para una demostración limpia es crear un Codespace nuevo sin
intervenir el anterior.

Si una instancia muestra la interfaz pero falla el backend, obtener evidencias
sin cambiar nada con bash scripts/doctor.sh. El diagnóstico del host,
limitaciones y criterios medibles se explican en
[11. Reproducibilidad Docker y Codespaces](docs/11_REPRODUCIBILIDAD_DOCKER.md).

**Datos:** no ejecutar docker compose down -v; elimina los volúmenes de
PostgreSQL y RabbitMQ. docker compose down sin -v los conserva.
