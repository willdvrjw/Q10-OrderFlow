# 03. Plan de validación y aceptación

No se marcará ningún elemento como verificado hasta haberlo ejecutado y guardado evidencia (salida de comandos, logs o prueba de UI). Esta lista está diseñada a partir de los criterios de aceptación del PDF.

## Checklist de aceptación obligatorio

- [ ] `docker compose up` desde copia limpia levanta **frontend, Orders API, Inventory Worker, RabbitMQ, PostgreSQL y seed**, sin pasos manuales extra.
- [ ] `GET /orders` responde por el puerto publicado del frontend y directamente en la red interna del API.
- [x] `POST /orders` responde 201 y devuelve `Pending` (comprobado vía REST).
- [x] Pedido con suficiente stock termina en `Confirmed` (comprobado vía REST).
- [x] Pedido con stock insuficiente termina en `Rejected` sin reducir stock (REST y PostgreSQL).
- [ ] Nombre en blanco, SKU inexistente y cantidad fuera de 1..100 retornan 400 con errores legibles.
- [ ] Los errores de formulario son visibles y comprensibles; no solo logs/console.
- [x] Un `OrderCreated` repetido no descuenta inventario dos veces (prueba PostgreSQL aislada aprobada).
- [x] Envíos concurrentes de cantidades incompatibles no producen stock negativo (prueba PostgreSQL aislada aprobada).
- [x] Si Inventory se detiene, el pedido permanece `Pending` y se procesa al recuperar el worker (pedido f069d769, comprobado en Codespaces por REST y Compose).
- [ ] Si RabbitMQ se detiene, la API puede seguir registrando pedidos `Pending` y reenviando al recuperar broker.
- [ ] El resultado persistido se recupera si hay una caída después de confirmar la transacción y antes del ACK.
- [ ] SignalR actualiza la interfaz y polling restaura coherencia tras desconexión.
- [ ] Los datos seed se crean automáticamente y reiniciar no restablece el stock consumido.
- [x] Las cinco pruebas backend se ejecutan con un solo comando documentado (20/10/2026, cinco aprobadas).
- [ ] Kubernetes incluye Deployment, Service, ConfigMap, probes y requests/limits de recursos.
- [ ] Un evaluador puede clonar el proyecto y estar operando en menos de 10 minutos en un host con Docker disponible y recursos suficientes.
- [ ] README y guía de defensa concuerdan con el código real, no con intenciones.

## Cinco pruebas backend propuestas

1. **Validación de entrada** (casos parametrizados): rechazo de cliente vacío/espacios, SKU desconocido y cantidad 0/101.
2. **Transición confirmada**: inicio en `Pending`, reserva de inventario y cambio permitido a `Confirmed`; transición terminal repetida sin efecto.
3. **Rechazo de stock**: producto con unidades insuficientes termina en `Rejected`, sin decrementar inventario.
4. **Idempotencia**: mismo pedido/evento dos veces; stock se descuenta una sola vez y solo hay un resultado de reserva.
5. **Concurrencia**: dos reservas para el mismo SKU y stock limitado; máximo una gana y el saldo nunca es negativo.

Algunos tests deben ejercitar una base PostgreSQL real para que la verificación del SQL atómico sea válida; una simulación en memoria no demostraría concurrencia verdadera. Los tests de dominio pueden ser unitarios y rápidos. El comando único de ejecución se definirá al implementar el entorno de test.

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
