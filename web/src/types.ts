// Contratos JSON públicos de Orders API: camelCase en la serialización.
export type OrderStatus = 'Pending' | 'Confirmed' | 'Rejected';

export interface Order {
  id: string;
  clienteNombre: string;
  sku: string;
  cantidad: number;
  estado: OrderStatus;
  creadoEn: string;
  actualizadoEn: string | null;
  motivoRechazo: string | null;
}

export interface Product {
  sku: string;
  nombre: string;
}

export interface CreateOrderRequest {
  clienteNombre: string;
  sku: string;
  cantidad: number;
}

export type FormErrors = Record<string, string[]>;
