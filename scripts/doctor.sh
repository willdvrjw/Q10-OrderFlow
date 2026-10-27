#!/usr/bin/env bash
# Diagnóstico de solo lectura: no recrea contenedores, no altera firewall ni bases.
set -u
cd "$(dirname "$0")/.."

echo "=== Versiones Docker / Compose ==="
docker compose version || exit 1
docker info --format 'Docker Engine: {{.ServerVersion}}' || exit 1

echo
echo "=== Configuración ==="
docker compose config --quiet || exit 1

echo
echo "=== Contenedores y healthchecks ==="
docker compose ps -a

echo
echo "=== Endpoints publicados: HTTP esperado 200 ==="
port=8080
if [ -n "$(printenv ORDERFLOW_WEB_PORT 2>/dev/null)" ]; then
  port=$(printenv ORDERFLOW_WEB_PORT)
fi
for path in / /health/ready /inventory/health/ready /orders /products /inventory; do
  printf '%-27s ' "$path"
  curl -sS --connect-timeout 3 --max-time 8 -o /dev/null \
    -w 'HTTP %{http_code}\n' "http://127.0.0.1:$port$path" || true
done

echo
echo "=== Comunicación desde Nginx hacia cada backend ==="
docker compose exec -T web sh -c \
  'wget -q -T 5 -O - http://orders-api:8080/health/ready' || echo "ERROR: Nginx no alcanza Orders"
echo
docker compose exec -T web sh -c \
  'wget -q -T 5 -O - http://inventory-worker:8081/health/ready' || echo "ERROR: Nginx no alcanza Inventory"

echo
echo "=== Logs recientes de arranque ==="
docker compose logs --no-color --tail=60 postgres rabbitmq orders-api inventory-worker web

echo
echo "Diagnóstico terminado. No se modificaron archivos, servicios, volúmenes ni firewall."
