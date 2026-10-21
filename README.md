# Q10-OrderFlow

Evaluación Fullstack: registro de pedidos, reserva asíncrona de inventario y panel de operaciones.

**Estado del proyecto:** solución de pedidos, inventario y alta dinámica de productos integrada en `main` y validada en GitHub Codespaces. Siete pruebas backend aprobadas y funcionamiento del alta, la reserva y el ajuste de existencias comprobado por el candidato.

- **Backend:** Orders API e Inventory Worker en .NET 10, RabbitMQ y PostgreSQL; pedidos `Pending`, `Confirmed` y `Rejected`.
- **Robustez:** Outbox transaccional, reintentos, idempotencia por pedido y descuento atómico; recuperación verificada ante detenciones de RabbitMQ y Worker.
- **Pruebas:** **siete pruebas backend aprobadas**: tres de dominio, dos de inventario y concurrencia sobre PostgreSQL y dos de creación dinámica/idempotencia de productos sobre PostgreSQL.
- **Frontend:** React + TypeScript con validación, filtrado, SignalR y recuperación por polling. Dependencias fijadas en `web/package-lock.json` e instaladas con `npm ci`.
- **Despliegue:** compilación, seed y creación/reserva comprobados en un clon independiente dentro del mismo Codespace; bonus Kubernetes ensamblado sintácticamente, no desplegado en un clúster.

Los resultados y límites de verificación están documentados en [evidencias de validación](docs/09_EVIDENCIAS_VALIDACION.md).

## Empezar: ejecución reproducible y enlaces de acceso

**Ver el código:** https://github.com/willdvrjw/Q10-OrderFlow

**Abrir un entorno desde el navegador:** https://codespaces.new/willdvrjw/Q10-OrderFlow?quickstart=1

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/willdvrjw/Q10-OrderFlow?quickstart=1)

> **Diferencia importante:** este repositorio es público, pero no existe una demo alojada de forma pública y permanente. El enlace de Codespaces permite que cada evaluador cree o reanude su propio entorno, con su propio puerto web y su propio inventario. Requiere cuenta y acceso/cuota de GitHub Codespaces; no equivale a una URL de aplicación ya desplegada.

### Opción A: Windows, macOS y Linux con Docker

**Requisito único de ejecución:** Docker Engine activo con Docker Compose v2. Instala [Docker Desktop](https://docs.docker.com/desktop/) en Windows/macOS o, en Linux, Docker Desktop o Docker Engine con el complemento Compose. En Windows usa contenedores Linux. No necesitas instalar .NET, Node.js, PostgreSQL ni RabbitMQ.

1. Obtén el repositorio con Git o mediante **Code > Download ZIP** de GitHub. Si usas Git:

   ~~~sh
   git clone https://github.com/willdvrjw/Q10-OrderFlow.git
   cd Q10-OrderFlow
   ~~~

   Con ZIP, descomprime y abre la terminal en la carpeta que contiene compose.yaml.

2. Inicia Docker y ejecuta **un solo comando** en la raíz del proyecto:

   ~~~sh
   docker compose up
   ~~~

3. Espera a que los servicios terminen de arrancar. Abre **http://localhost:8080** en el navegador. El estado de API se puede comprobar en **http://localhost:8080/health/ready** (debe mostrar la propiedad status en ready). Crea un pedido desde el panel y observa su estado Pending hasta que Inventory lo confirme o rechace.

La primera ejecución descarga y construye las imágenes y puede tardar varios minutos; la velocidad de red y el hardware pueden alargar el tiempo. El objetivo del README es que el proceso **se entienda** en menos de diez minutos, no prometer un tiempo de descarga universal.

### Opción B: navegador con GitHub Codespaces, sin Docker instalado localmente

1. Abre [Crear/reanudar Codespace en main](https://codespaces.new/willdvrjw/Q10-OrderFlow?quickstart=1) e inicia sesión en GitHub.
2. Cuando se abra el editor web y su terminal, ejecuta:

   ~~~sh
   docker compose up -d --build
   ~~~

3. Abre **Ports / Puertos** y pulsa **Open in Browser / Abrir en navegador** sobre el puerto 8080. GitHub asignará una URL de tu propio Codespace.

El código puede consultarse sin iniciar sesión porque GitHub lo sirve desde el repositorio público. En cambio, crear Codespaces requiere acceso a GitHub; el puerto suele ser **privado** y la sesión/URL dependen de ese entorno.

**Seguridad del enlace:** la consola administrativa de la demo no tiene autenticación (no es un requisito de Q10). Por ello **no publiques el puerto de Codespaces ni expongas este sistema a Internet con datos reales**: visitantes anónimos podrían crear pedidos y modificar existencias. Una demo pública permanente necesitaría infraestructura hospedada y protección de las operaciones de escritura; no se proporciona ni se afirma que exista actualmente.

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
- **Codespaces no está disponible:** depende de la cuota/cuenta GitHub; usa la opción A. El Codespace de desarrollo necesitó resolver una peculiaridad de firewall del host, que **no** es un paso estándar en Docker Desktop.
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

**Alcance de validación:** el sistema se ejecutó desde una copia nueva dentro de GitHub Codespaces, con volúmenes independientes. El firewall específico de ese entorno requirió un ajuste local de conectividad entre contenedores. No se ha probado todavía en un host Docker diferente. La emulación responsive original se verificó a 530 × 710. Para la ampliación del inventario el usuario confirmó visualmente la vista móvil a 390 px sin aportar nueva captura; no equivale a un ensayo independiente de estrés con muchos SKU. Kubernetes se verificó mediante Kustomize, sin clúster. Ver `docs/09_EVIDENCIAS_VALIDACION.md`.
