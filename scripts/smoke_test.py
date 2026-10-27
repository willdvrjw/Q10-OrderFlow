#!/usr/bin/env python3
"""Verificación real HTTP para CI: catálogo, inventario, pedido y persistencia.

Usa únicamente la biblioteca estándar de Python. CREA un producto y un pedido
de prueba; ejecutar exclusivamente sobre una instancia descartable.
"""
import argparse
import json
import sys
import time
import uuid
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


def http(base: str, path: str, *, method: str = "GET",
         payload: dict | None = None, status: int = 200):
    data = json.dumps(payload).encode("utf-8") if payload is not None else None
    headers = {"Accept": "application/json"}
    if data is not None:
        headers["Content-Type"] = "application/json"
    request = Request(base + path, data=data, headers=headers, method=method)
    try:
        with urlopen(request, timeout=12) as response:
            actual = response.status
            raw = response.read().decode("utf-8")
    except HTTPError as error:
        actual = error.code
        raw = error.read().decode("utf-8", errors="replace")
    except URLError as error:
        raise AssertionError(f"{method} {path}: API inaccesible: {error}") from error

    if actual != status:
        raise AssertionError(
            f"{method} {path}: esperaba HTTP {status}, obtuvo {actual}: {raw[:500]}")
    if path == "/" and method == "GET":
        return raw
    return json.loads(raw) if raw.strip() else None


def assert_health(base: str) -> None:
    assert "id=\"root\"" in http(base, "/"), "El HTML de React no está publicado"
    assert http(base, "/health/ready")["status"] == "ready", "Orders no está listo"
    assert http(base, "/inventory/health/ready")["status"] == "ready", "Inventory no está listo"


def available(base: str, sku: str) -> int:
    inventory = http(base, "/inventory")
    matching = [item for item in inventory if item["sku"] == sku]
    assert len(matching) == 1, f"Inventario inesperado para {sku}: {matching}"
    return matching[0]["disponible"]


def assert_persistence(base: str, evidence: dict) -> None:
    assert_health(base)
    sku = evidence["sku"]
    assert any(p["sku"] == sku for p in http(base, "/products")), "Producto desaparecido"
    assert available(base, sku) == evidence["remaining"], "Stock cambió después del reinicio"
    order = http(base, "/orders/" + evidence["order_id"])
    assert order["estado"] == "Confirmed", f"Estado persistido inesperado: {order}"
    matches = [o for o in http(base, "/orders") if o["id"] == evidence["order_id"]]
    assert len(matches) == 1, "El pedido desapareció o está duplicado"
    print(f"OK persistencia: {sku}, pedido {evidence['order_id']}, stock {evidence['remaining']}")


def exercise(base: str) -> dict:
    assert_health(base)
    initial_products = http(base, "/products")
    assert len(initial_products) >= 3, "Los productos iniciales no están disponibles"

    sku = "QA-" + uuid.uuid4().hex[:10].upper()
    product = {"sku": sku, "nombre": "Producto temporal de integración", "disponible": 9}
    created = http(base, "/products", method="POST", payload=product, status=201)
    assert created["sku"] == sku, f"Alta incorrecta: {created}"
    assert any(p["sku"] == sku for p in http(base, "/products")), "Catálogo sin nuevo SKU"
    assert available(base, sku) == 9, "Inventory no registró las existencias iniciales"

    http(base, "/products", method="POST", payload=product, status=409)
    assert available(base, sku) == 9, "El SKU duplicado modificó el stock"

    order = http(base, "/orders", method="POST", status=201, payload={
        "clienteNombre": "Integracion Docker CI", "sku": sku, "cantidad": 3})
    assert order["estado"] == "Pending", f"El pedido no inició Pending: {order}"

    deadline = time.monotonic() + 60
    while time.monotonic() < deadline:
        current = http(base, "/orders/" + order["id"])
        if current["estado"] == "Confirmed":
            break
        assert current["estado"] == "Pending", f"Reserva rechazada: {current}"
        time.sleep(1)
    else:
        raise AssertionError("RabbitMQ/Inventory no confirmó el pedido en 60 segundos")

    assert available(base, sku) == 6, "La reserva no descontó exactamente 3 unidades"
    evidence = {"sku": sku, "order_id": order["id"], "remaining": 6}
    assert_persistence(base, evidence)
    print("OK E2E: React, 2 backends, PostgreSQL, RabbitMQ, altas, 409, reserva")
    return evidence


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-url", default="http://127.0.0.1:8080")
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--verify", action="store_true")
    args = parser.parse_args()
    base = args.base_url.rstrip("/")
    if args.verify:
        assert_persistence(base, json.loads(args.evidence.read_text(encoding="utf-8")))
    else:
        evidence = exercise(base)
        args.evidence.write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (AssertionError, OSError, ValueError, KeyError) as error:
        print(f"FALLO QA: {error}", file=sys.stderr)
        sys.exit(1)
