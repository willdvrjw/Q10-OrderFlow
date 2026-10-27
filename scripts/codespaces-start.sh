#!/usr/bin/env bash
# Ejecutado por Dev Containers en cada inicio; nunca elimina los datos Docker.
set -Eeuo pipefail
cd "$(dirname "$0")/.."

for attempt in $(seq 1 45); do
  if docker info >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

if ! docker info >/dev/null 2>&1; then
  echo "ERROR: El motor Docker del devcontainer no responde." >&2
  exit 1
fi

docker compose config --quiet

if ! docker compose up -d --build --wait --wait-timeout 240; then
  echo "ERROR: Uno o más servicios no está saludable. Diagnóstico:" >&2
  docker compose ps -a || true
  docker compose logs --no-color --tail=90 || true
  exit 1
fi

echo "OrderFlow está listo en el puerto 8080 (reenvío privado de Codespaces)."
docker compose ps
