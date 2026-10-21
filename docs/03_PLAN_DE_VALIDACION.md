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

- [ ] Construir el Worker con ASP.NET Core y consultar `GET /inventory` a través de Nginx.
- [ ] Ejecutar `POST /inventory/adjustments` para agregar unidades, establecer existencias y rechazar un saldo obsoleto con HTTP 409.
- [x] Comprobar que un ajuste de existencias conserva los siete pedidos previos y sus estados (capturas: 7, 6 Confirmed, 1 Rejected).
- [ ] Comprobar una nueva reserva después de los ajustes de stock.
- [ ] Ejecutar nuevamente las cinco pruebas backend: el test de concurrencia ahora añade aserciones para las operaciones administrativas.
- [ ] Revisar la imagen de portada aportada por el candidato, el sombreado de contraste, degradados, figuras discretas, responsive y scroll interno del listado de pedidos.
- [x] Comprobar señalización WebSocket de inventario: conexiones `/hubs/inventory` y `/hubs/orders` con HTTP 101; el usuario confirmó recepción del mensaje `inventoryChanged` al ajustar existencias desde una segunda pestaña, sin recargar la receptora. Confirmación comunicada, sin captura del frame final.
- [ ] Comprobar el mensaje `inventoryChanged` generado específicamente por una reserva nueva.
- [ ] Comprobar recuperación de existencias por polling de 10 s al bloquear exclusivamente SignalR de inventario.
- [ ] Verificar que un pedido `Rejected` conserva su estado tras reponer stock y que «Crear nuevo pedido» únicamente precarga otro formulario sin enviarlo automáticamente.
- [ ] Fusionar la rama de cambios en `main` solamente después de compilar y comprobar el flujo.

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
