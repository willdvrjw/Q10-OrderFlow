# 03. Plan de validación y aceptación

No se marcará ningún elemento como verificado hasta haberlo ejecutado y guardado evidencia (salida de comandos, logs o prueba de UI). Esta lista está diseñada a partir de los criterios de aceptación del PDF.

## Checklist de aceptación obligatorio

- [x] Una copia limpia en el mismo Codespace, arrancada con `docker compose up -d --build`, construyó frontend, Orders API, Inventory Worker, RabbitMQ y PostgreSQL; se validó seed y pedido Confirmed. Fue necesaria una regla temporal de firewall del host.
- [ ] Verificar el comando literal `docker compose up` y portabilidad a otro host Docker sin ajustes especiales.
- [x] `GET /orders` respondió por el puerto publicado (HTTP 200) y desde el contenedor web mediante la red interna Docker; 7 pedidos originales conservados.
- [x] `POST /orders` responde 201 y devuelve `Pending` (comprobado vía REST).
- [x] Pedido con suficiente stock termina en `Confirmed` (comprobado vía REST).
- [x] Pedido con stock insuficiente termina en `Rejected` sin reducir stock (REST y PostgreSQL).
- [x] Nombre en blanco, SKU inexistente y cantidad fuera de 1..100 retornan 400 con errores legibles (probado vía HTTP; 3 respuestas application/problem+json).
- [x] Los errores de formulario son visibles y comprensibles (captura: nombre y SKU vacíos con mensajes bajo los campos).
- [x] Un `OrderCreated` repetido no descuenta inventario dos veces (prueba PostgreSQL aislada aprobada).
- [x] Envíos concurrentes de cantidades incompatibles no producen stock negativo (prueba PostgreSQL aislada aprobada).
- [x] Si Inventory se detiene, el pedido permanece `Pending` y se procesa al recuperar el worker (pedido f069d769, comprobado en Codespaces por REST y Compose).
- [x] Si RabbitMQ se detiene, la API registra pedidos `Pending` y el procesamiento se recupera al reiniciar el broker (pedido bca788f1 confirmado, stock XYZ-02 = 23).
- [ ] El resultado persistido se recupera si hay una caída después de confirmar la transacción y antes del ACK.
- [x] Creación real desde formulario web y actualización automática de tabla y contadores sin recarga manual (ABC6E820).
- [x] Conexión SignalR por WebSocket HTTP 101 y frame `ordersChanged` (`type:1`) recibido en Chrome DevTools.
- [x] Polling REST restaura coherencia al bloquear SignalR: 1 solicitud afectada; contadores 6→7 y confirmados 5→6 sin recarga manual.
- [x] Los datos seed se crean automáticamente y reiniciar no restablece el stock consumido (cinco pedidos idénticos antes y después; diff de inventario vacío).
- [x] Las cinco pruebas backend se ejecutan con un solo comando documentado (20/10/2026, cinco aprobadas).
- [x] Kubernetes incluye Deployment, Service, ConfigMap, probes y requests/limits; `kubectl kustomize k8s` generó los tres recursos sin errores. No se ha validado contra un clúster.
- [ ] Verificar de forma independiente arranque en otro host Docker y tiempo total hasta funcionamiento; aquí se comprobó un clon local al Codespace en el que las imágenes se compilaron en ~52 s, pero el arranque y la corrección de red exigieron tiempo adicional.
- [x] README, matriz y guía de defensa contrastados con archivos reales; eliminadas rutas y afirmaciones de trabajo futuro obsoletas.

## Ampliación solicitada de interfaz e inventario — pendiente de validación

- [x] Worker ASP.NET Core compilado; `GET /inventory` respondió HTTP 200 con los tres SKU y se comprobó negociación SignalR HTTP 200.
- [x] Registrar ingreso y reemplazo de existencias desde la interfaz (`TEST-03` 3→6 y posterior ajuste de total); respuesta visual confirmada y actualización de tarjetas.
- [x] Comprobar HTTP 409 de `POST /inventory/adjustments` con `existenciaEsperada: 999999`: el servidor respondió `409 Conflict`, `disponibleActual: 6`, y se verificó que los tres saldos no cambiaron (`ABC-01=96`, `TEST-03=6`, `XYZ-02=20`).
- [x] Comprobar que un ajuste de existencias conserva los siete pedidos previos y sus estados (capturas: 7, 6 Confirmed, 1 Rejected).
- [x] Se observó una reserva nueva de `TEST-03` ×5 confirmada tras actualizar inventario a 6: panel con 9 pedidos, 7 confirmados y 2 rechazados. La creación de esa solicitud con cantidad cambiada reveló otro fallo del flujo de repetición, que se corrigió posteriormente.
- [x] El comando `docker compose --profile tests run --build --rm backend-tests` volvió a pasar 5/5 después de incorporar SignalR de inventario y sus aserciones (registro compartido por el candidato).
- [x] Revisado visualmente en capturas: portada fotográfica con contraste, tarjetas, diseño móvil a 530 px, altura y scroll interno de seguimiento, y encabezados de SKU.
- [x] Confirmación expresa del usuario: el aviso se cierra manualmente mediante X y los errores rojos están centrados en el formulario.
- [x] Validación final en Codespaces: `docker compose --profile tests run --build --rm backend-tests` ejecutó **5/5 pruebas aprobadas**; `docker compose up -d --build --no-deps web` completó sin errores reutilizando correctamente sus capas de caché (no fue build sin caché); `GET /health/ready` devolvió ready, 10 pedidos (8 Confirmed, 2 Rejected), y el repositorio local estaba limpio.
- [x] Comprobar señalización WebSocket de inventario: conexiones `/hubs/inventory` y `/hubs/orders` con HTTP 101; el usuario confirmó recepción del mensaje `inventoryChanged` al ajustar existencias desde una segunda pestaña, sin recargar la receptora. Confirmación comunicada, sin captura del frame final.
- [ ] Comprobar el mensaje `inventoryChanged` generado específicamente por una reserva nueva.
- [ ] Comprobar recuperación de existencias por polling de 10 s al bloquear exclusivamente SignalR de inventario.
- [ ] Comprobar que «Crear nuevo pedido» **permanece deshabilitado** cuando la cantidad de existencias es menor que la solicitada, y se habilita automáticamente tras una reposición suficiente.
- [x] Comprobar bloqueo del envío desde un rechazado cuando la cantidad solicitada excede el stock: TEST-03 solicitó 5 con 4 disponibles; error visible y 8 pedidos conservados. La implementación usa GET /inventory con `cache: no-store` antes del POST.
- [ ] Comprobar el escenario de fallo de consulta y la habilitación/deshabilitación del botón al variar el stock, y crear finalmente una reserva exitosa con cantidad disponible.
- [x] **Repetición exacta:** a partir de un rechazado original por 4 unidades, cambiar la cantidad a 3 mostró error rojo inmediato y «Confirmar repetición» deshabilitado, con 9 pedidos sin aumento (captura compartida). El caso 4→5 fue la causa original de la corrección.
- [ ] Volver la cantidad a 4 debe permitir confirmar la repetición, previa verificación nueva de GET /orders/{id} y GET /inventory; el pedido original debe permanecer Rejected.
- [ ] «Cancelar repetición» debe restaurar el registro manual; el flujo normal de Q10 no debe quedar bloqueado para cantidades distintas.
- [x] El panel mostró 9 pedidos: 7 confirmados y 2 rechazados; los rechazados conservaron su estado tras la reposición. La creación de una solicitud nueva confirmada se observó antes de imponer la equivalencia estricta.
- [ ] Validar una repetición exacta (mismo cliente, SKU y cantidad), sin reutilizar el identificador anterior, en un ensayo controlado.
- [ ] Comprobar que el formulario normal sigue aceptando un pedido válido insuficiente con HTTP 201 Pending, para que Inventory lo resuelva a Rejected según Q10.
- [x] Integrar por **fast-forward** `feat/inventory-and-ui-refinement` a `main` después de las cinco pruebas backend aprobadas, frontend reconstruido mediante Docker, HTTP de API e inventario correcto y confirmaciones manuales de la interfaz. No se reescribió historial ni se eliminaron volúmenes.

## Ampliación: alta dinámica de productos y limpieza de Chrome — 21/10/2026

**Origen:** `feat/dynamic-products-and-console-quality`, integrada a `main` mediante fast-forward el 21/10/2026. Ampliación compilada y verificada en Codespaces: siete pruebas backend aprobadas, APIs HTTP 200, creación de producto y pedido comprobada por el usuario, protección de SKU duplicados y persistencia de catálogo, inventario y pedidos tras reinicio. Los escenarios no observados permanecen explícitamente pendientes.

- [x] Compilar `orders-api`, `inventory-worker` y `web` desde esta rama con Docker Compose: tres imágenes compiladas sin errores C# o TypeScript; tras corregir DNS dinámico en Nginx se obtuvieron **HTTP 200** en los tres endpoints y `nginx -t` correcto. El error 502 inicial quedó resuelto.
- [x] Ejecutar **7 pruebas** (5 originales + 2 nuevas PostgreSQL): tras restablecer la comunicación Docker con la regla `DOCKER-USER` específica del puente `br-d944a5fdf444`, la segunda ejecución terminó con **7 aprobadas, 0 fallidas**. La primera falló con cuatro timeouts de conexión a PostgreSQL (no fallos de lógica).
- [x] Tras reconstruir únicamente `web` con resolución DNS dinámica de Docker, `nginx -t` declaró configuración correcta y `GET /health/ready`, `GET /products`, `GET /inventory` devolvieron **HTTP 200**, con los tres SKU y el stock original (`ABC-01=96`, `TEST-03=6`, `XYZ-02=20`). Los logs de Nginx corroboran las tres solicitudes 200.
- [ ] Verificar además negociación y actualización de ambos hubs SignalR tras corregir la resolución Docker; la comprobación HTTP anterior no equivale a comprobar WebSockets.
- [ ] Consultar `GET /orders` y `GET /inventory` antes y después del despliegue: preservar los 10 pedidos existentes y saldos conocidos de `ABC-01`, `TEST-03` y `XYZ-02`; no borrar volúmenes.
- [x] **Prueba manual comunicada:** el usuario creó un producto desde **Agregar producto nuevo**; apareció tanto en el selector de **Nuevo pedido** como en **Actualizar existencias**. El usuario también consiguió modificar su stock y generar un pedido. No se recibió la respuesta HTTP 201 ni los valores del producto en esta comprobación.
- [ ] Capturar respuesta HTTP 201, JSON de `GET /products` y `GET /inventory` para documentar identificador exacto, SKU y existencias iniciales.
- [x] **Prueba manual comunicada:** el usuario ajustó las existencias del producto nuevo, creó un pedido para ese producto y confirmó que el inventario se actualizó correctamente y el flujo funcionó. No se recibieron las cifras antes/después ni evidencia del frame SignalR.
- [ ] Verificar vía REST la transición `Pending → Confirmed` del SKU nuevo, su descuento exacto, el rechazo por falta de existencias sin stock negativo y la notificación por SignalR.
- [x] Comprobar manualmente que **Agregar producto nuevo** no permite guardar un SKU duplicado: el usuario confirmó expresamente que el formulario impide el guardado.
- [x] Probar `POST /products` directo con el SKU dinámico existente `FRP` en minúsculas (`frp`): **HTTP 409 Conflict**, mensaje «El SKU ya está registrado. Utiliza uno distinto.» y comparaciones `cmp -s` idénticas antes/después de `GET /products`, `GET /inventory` y `GET /orders`. Queda validada la unicidad del backend sin modificar información.
- [x] Reiniciar sin `down -v` los servicios `orders-api`, `inventory-worker` y `web`, comparando instantáneas reales antes/después de `GET /products`, `GET /inventory` y `GET /orders`: los **tres archivos resultaron idénticos** (`SIN CAMBIOS - CORRECTO`). El catálogo conserva el nuevo producto **Foto Retablos Personalizados (`FRP`)** junto a los tres SKU de la semilla; cinco contenedores activos después del reinicio.
- [x] **Comprobación visual comunicada por el usuario:** diseño de Inventario disponible correcto tanto en escritorio como en vista móvil, con área desplazable, altura equilibrada y botón de alta debajo de tarjetas. El usuario confirmó «están bien, confía en mí», sin enviar nuevas capturas.
- [ ] Ensayar explícitamente un catálogo con muchos SKU que obligue a desplazamiento prolongado; la aprobación visual anterior corresponde al catálogo actual.
- [x] Aprobación visual comunicada por el usuario: el texto introductorio de «Actualizar existencias» está correctamente alineado a la izquierda.
- [ ] Volver a inspeccionar Problemas de Chrome: el buscador de pedidos ya incluye `id`/`name` y los campos de Inventory también; comprobar la desaparición de esa advertencia con recarga nueva.
- [ ] Investigar el error asíncrono de `chrome.runtime` en un perfil sin extensiones para separar errores del navegador/terceros de errores de la aplicación. No silenciar la consola globalmente ni atribuir mensajes de extensiones a React sin evidencia.
- [x] Cerrar la validación manual solicitada: siete pruebas aprobadas, alta/pedido/stock, unicidad API HTTP 409, persistencia idéntica de tres recursos tras reinicio y aprobación visual de escritorio/móvil.
- [x] Integrar por **fast-forward** `feat/dynamic-products-and-console-quality` en `main` tras aprobar siete pruebas automáticas, flujo de alta/productos/inventario, SKU duplicado HTTP 409, persistencia y validación visual del usuario. Las entidades `Order`, `OrderStatus`, el manejador de reservas y los contratos de eventos originales permanecieron sin cambios. Se documentaron separadamente los ensayos adicionales no realizados.

## Cinco pruebas backend implementadas y verificadas

1. **Validación de entrada**: rechazo de nombre vacío, SKU desconocido y cantidad fuera del rango permitido mediante aserciones dentro del test de dominio.
2. **Transición confirmada**: inicio en `Pending`, cambio permitido a `Confirmed` y prohibición de revertirlo (test del dominio, sin invocar Inventory).
3. **Estado rechazado terminal**: un pedido `Rejected` no puede pasar después a `Confirmed` (test de dominio). El rechazo por falta de stock sin descuento se verificó aparte mediante REST y consulta SQL.
4. **Idempotencia**: mismo `OrderId` recibido dos veces con `EventId` diferente; stock se descuenta una sola vez y solo se genera un resultado de reserva (PostgreSQL real).
5. **Concurrencia**: dos reservas para el mismo SKU y stock limitado; máximo una gana y el saldo nunca es negativo.

Las dos pruebas de persistencia utilizan PostgreSQL real, contextos independientes y bases temporales aisladas. Se ejecutaron con éxito junto a las tres pruebas de dominio mediante `docker compose --profile tests run --build --rm backend-tests` (cinco aprobadas). Una simulación en memoria no demostraría la concurrencia real.

## Ensayos de fallos (manuales guiados)

A. Parar Inventory, crear pedido, confirmar `Pending`, arrancar Inventory y confirmar transición eventual.
B. Parar RabbitMQ después de levantar el sistema, crear pedido, confirmar 201 + `Pending`, arrancar broker y confirmar publicación pendiente.
C. Volver a entregar `OrderCreated` con igual `orderId`; verificar que el stock no cambia otra vez.
D. Generar dos pedidos casi simultáneos para un SKU con stock para solo uno.
E. Reiniciar contenedores con volumen persistente; verificar que pedidos/stock no se reinicializan.
F. Cortar conectividad del navegador a SignalR; verificar recuperación con polling.
G. Probar desktop y móvil, teclado, mensajes de error y pantallas vacías.

## Criterios de revisión de ingeniería

- Separa responsabilidades sin circularidad entre proyectos.
- Los nombres y contratos son coherentes; JSON en camelCase.
- Transacciones y `CancellationToken` bien gestionados.
- No silencia excepciones ni registra secretos.
- Tiene logs estructurados con `orderId` y `eventId` para correlación.
- Usa variables de entorno y Dockerfiles multi-stage.
- Mantiene dependencias de imagen, SDK y paquetes compatibles.
- No confunde simulación de falla con resultado de prueba aprobado.
- README incluye trade-offs y «Qué haría distinto con más tiempo».

## Prioridad de defectos

**Bloqueante:** no compila, no arranca Compose, falla flujo base, descuenta doble, stock negativo, pérdida de pedidos.
**Alto:** error de validación incorrecto, no recupera broker, tests fallan, transición terminal inválida.
**Medio:** fallback de polling, manifest K8s o problema responsive relevante.
**Bajo:** microdetalles visuales que no afecten uso.

Registrar cada resultado real tras ejecutar: fecha, comando, resultado esperado, resultado observado, corrección y nueva comprobación.
