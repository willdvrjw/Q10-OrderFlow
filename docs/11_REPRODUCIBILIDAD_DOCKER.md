# 11. Reproducibilidad Docker, Codespaces y validación independiente

## Incidencia documentada

El 26/10/2026, la interfaz de OrderFlow cargaba desde el puerto 8080 de un
Codespace, pero fallaban las consultas de /orders, /products y /inventory.
Los contadores "0" no probaban que los pedidos hubieran sido eliminados:
React inicializa los arreglos vacíos cuando no puede consultar las APIs.

Los registros del 21/10/2026 documentaban un fallo anterior de conectividad
entre contenedores Docker y PostgreSQL en el mismo Codespace, debido a reglas
de firewall FORWARD DROP; se recuperó temporalmente con una regla DOCKER-USER.
También se observó un error de DNS de Nginx corregido en main mediante
resolución dinámica. La causa actual exacta de la captura no pudo
confirmarse sin acceso a los procesos, redes y registros del Codespace activo.

**Separación fundamental:** el firewall del host es responsabilidad del host y
de Docker Engine, no de Orders API. Compose no puede garantizar que un motor
Docker mal configurado funcione; no es aceptable desactivar reglas globales,
conceder privilegios elevados a la aplicación o modificar el firewall
silenciosamente para que una demo parezca saludable.

Referencias oficiales:
- https://docs.docker.com/engine/network/packet-filtering-firewalls/
- https://docs.docker.com/engine/network/firewall-iptables
- https://docs.docker.com/compose/how-tos/startup-order/
- https://docs.docker.com/reference/cli/docker/compose/up/

## Correcciones incorporadas al repositorio

1. PostgreSQL pasa a saludable únicamente cuando responde por TCP y las dos
   bases lógicas orders/inventory existen y aceptan una consulta autenticada.
   Evita un falso positivo del socket local durante la inicialización.
2. RabbitMQ es dependencia healthy, no simplemente un proceso iniciado.
3. Orders API e Inventory Worker tienen healthchecks que ejecutan el mismo
   binario .NET en modo comprobación (sin iniciar otro servidor) y consultan
   el /health/ready real, incluyendo conexión a su PostgreSQL.
4. La web espera que ambos backends estén saludables antes de arrancar. Su
   propio healthcheck verifica Orders **e Inventory** mediante Nginx, no solo
   que exista una página HTML estática.
5. Nginx conserva el proxy dinámico ya validado. Se añade una ruta exacta
   /inventory/health/ready para observar la salud de Inventory.
6. .devcontainer/devcontainer.json usa la característica oficial
   Docker-in-Docker, con motor independiente para nuevos Codespaces;
   postStartCommand arranca Compose y espera salud real en cada apertura.
7. Un workflow de GitHub Actions ejecuta Compose desde checkout limpio,
   realiza una prueba E2E (alta, idempotencia de SKU, pedido, descuento,
   PostgreSQL, RabbitMQ), ejecuta siete tests e inspecciona persistencia
   después de reiniciar. Ante fallos publica logs y deja la validación roja.
8. scripts/doctor.sh proporciona evidencia de solo lectura en un entorno
   actualmente defectuoso. No cambia puertos, reglas, volúmenes ni pedidos.

## Evaluador externo (procedimiento principal)

Prerequisitos: Docker Engine + Compose v2 con capacidad de usar contenedores
Linux; conexión para descargar imágenes de base y restaurar paquetes en
la primera compilación. Se recomienda memoria libre suficiente para cinco
contenedores. Sin instalar .NET, Node, PostgreSQL ni RabbitMQ en el equipo:

    git clone https://github.com/willdvrjw/Q10-OrderFlow.git
    cd Q10-OrderFlow
    docker compose config --quiet
    docker compose up -d --build --wait --wait-timeout 240

Abrir http://127.0.0.1:8080 y comprobar que ambas rutas devuelven
{"status":"ready"}:

    http://127.0.0.1:8080/health/ready
    http://127.0.0.1:8080/inventory/health/ready

La prueba exigida por Q10 sigue siendo compatible con:

    docker compose up

El comando con --wait es la variante que además comprueba automáticamente
readiness y devuelve fallo si una dependencia permanece inaccesible.

Ejecutar siete pruebas backend:

    docker compose --profile tests run --build --rm backend-tests

Los datos se conservan con:

    docker compose down
    docker compose up -d --wait

No ejecutar "docker compose down -v": eliminaría los volúmenes de demostración.

## Codespaces (opcional; NO es la vía contractual de evaluación)

Los Codespaces nuevos creados después de incluir .devcontainer utilizan un
Docker Engine aislado dentro del entorno de desarrollo y arrancan el stack
al inicio. El puerto 8080 se reenvía de forma privada a la sesión GitHub.
Es una demo temporal, no un despliegue público permanente. Si el Codespace
se suspende, no se puede prometer disponibilidad de su URL de previsualización.

Un Codespace **existente** no adopta automáticamente su nueva configuración.
Según la documentación de GitHub, hay que actualizar el repositorio y
reconstruir el contenedor de desarrollo. Precaución: cambiar de motor Docker
puede dejar volúmenes históricos del motor anterior fuera de la nueva vista,
aunque los archivos bajo /workspaces sigan presentes. Antes de reconstruir
un Codespace con información útil, hacer respaldo de PostgreSQL/RabbitMQ o
crear un Codespace nuevo para la demostración sin tocar el anterior.

Documentación GitHub:
- https://docs.github.com/en/codespaces/developing-in-a-codespace/rebuilding-the-container-in-a-codespace

## Diagnóstico de un fallo, sin operaciones destructivas

En el entorno que muestra errores, ejecutar desde la raíz:

    bash scripts/doctor.sh

La salida verifica la disponibilidad de Docker, Compose, HTTP público,
comunicación directa de Nginx hacia Orders/Inventory y logs de los cinco
servicios. Si los contenedores se inician pero no se comunican y el firewall
del host bloquea el puente, reparar Docker Engine / firewall con los
procedimientos de su administrador. No incorporar reglas manuales de
iptables al arranque de la aplicación.

## Criterio de aceptación objetivo

- Compose en entorno limpio construye y muestra los cinco servicios sanos.
- HTTP 200 real en web y ambos endpoints de readiness.
- Productos, pedidos e inventario consultables simultáneamente.
- Producto dinámico nuevo se refleja en Orders e Inventory.
- Duplicar SKU responde 409 sin alterar existencias.
- Pedido válido inicia Pending y termina Confirmed por RabbitMQ; stock
  disminuye exactamente una vez.
- Suite .NET de siete tests termina aprobada.
- Reiniciar preserva pedido, producto y stock.
- Registro de Actions en verde disponible como evidencia inspeccionable.

El workflow permite verificar estas condiciones en un runner Docker limpio.
No equivale a certificar todo ordenador posible, ni implica que el Codespace
privado del usuario haya sido observado o reparado directamente.
