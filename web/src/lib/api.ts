import type { CreateOrderRequest, FormErrors, InventoryAdjustmentRequest, InventoryAdjustmentResponse, InventoryStock, Order, Product } from '../types';

interface ProblemDetails {
  mensaje?: string;
  title?: string;
  detail?: string;
  errors?: FormErrors;
}

/**
 * Error HTTP estructurado. La vista muestra los mensajes de validación
 * junto a los campos y nunca se limita a registrarlos en la consola.
 */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly errors: FormErrors = {},
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);
  headers.set('Accept', 'application/json');
  if (init?.body) headers.set('Content-Type', 'application/json');

  // Las rutas relativas aprovechan el mismo origen a través de Nginx.
  const response = await fetch(path, { ...init, headers });

  if (!response.ok) {
    let problem: ProblemDetails = {};
    try {
      problem = (await response.json()) as ProblemDetails;
    } catch {
      // Proxies pueden devolver errores sin JSON; conservamos el estado HTTP.
    }

    throw new ApiError(
      response.status,
      problem.mensaje ?? problem.detail ?? problem.title ?? 'Solicitud no completada (' + response.status + ').',
      problem.errors ?? {},
    );
  }

  return (await response.json()) as T;
}

export function getOrders(): Promise<Order[]> {
  return request<Order[]>('/orders');
}

export function getProducts(): Promise<Product[]> {
  return request<Product[]>('/products');
}

export function createOrder(body: CreateOrderRequest): Promise<Order> {
  return request<Order>('/orders', {
    method: 'POST',
    body: JSON.stringify(body),
  });
}

/** Estas rutas van al servicio Inventory a través de Nginx, no a Orders. */
export function getInventory(): Promise<InventoryStock[]> {
  return request<InventoryStock[]>('/inventory');
}

export function adjustInventory(body: InventoryAdjustmentRequest): Promise<InventoryAdjustmentResponse> {
  return request<InventoryAdjustmentResponse>('/inventory/adjustments', {
    method: 'POST',
    body: JSON.stringify(body),
  });
}
