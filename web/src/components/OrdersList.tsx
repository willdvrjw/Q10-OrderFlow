import { useMemo, useState } from 'react';
import type { Order, OrderStatus } from '../types';

type StatusFilter = 'All' | OrderStatus;

interface Props {
  orders: Order[];
  loading: boolean;
  error: string | null;
  onRefresh: () => void;
  height?: number;
  onRepeat: (order: Order) => void;
}

const filters: { value: StatusFilter; label: string }[] = [
  { value: 'All', label: 'Todos' },
  { value: 'Pending', label: 'Pendientes' },
  { value: 'Confirmed', label: 'Confirmados' },
  { value: 'Rejected', label: 'Rechazados' },
];

const statusText: Record<OrderStatus, string> = {
  Pending: 'Pendiente',
  Confirmed: 'Confirmado',
  Rejected: 'Rechazado',
};

function StatusBadge({ status }: { status: OrderStatus }) {
  return <span className={'status-badge ' + status.toLowerCase()}>{statusText[status]}</span>;
}

function formatDate(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? 'Fecha desconocida'
    : new Intl.DateTimeFormat('es-CO', { dateStyle: 'short', timeStyle: 'short' }).format(date);
}

/**
 * Tabla de lectura y filtros locales. Los estados se consultan desde Orders API;
 * el frontend nunca decide si una reserva se confirma o rechaza.
 */
export function OrdersList({ orders, loading, error, onRefresh, height, onRepeat }: Props) {
  const [filter, setFilter] = useState<StatusFilter>('All');
  const [search, setSearch] = useState('');

  const visible = useMemo(() => {
    const term = search.trim().toLowerCase();
    return orders.filter(order =>
      (filter === 'All' || order.estado === filter) &&
      (!term || [order.clienteNombre, order.sku, order.id].some(value =>
        value.toLowerCase().includes(term))),
    );
  }, [orders, filter, search]);

  return (
    <section className="panel orders-panel" aria-labelledby="orders-title" style={{ height: height ? height + "px" : undefined }}>
      <div className="panel-heading">
        <div>
          <span className="eyebrow">ACTIVIDAD RECIENTE</span>
          <h2 id="orders-title">Seguimiento de pedidos</h2>
          <p>Visualiza su progreso y resultado actualizado.</p>
        </div>
        <button className="refresh-button" type="button" onClick={onRefresh} aria-label="Actualizar pedidos">
          <span aria-hidden="true">↻</span> Actualizar
        </button>
      </div>

      <div className="orders-toolbar">
        <div className="filter-tabs" role="group" aria-label="Filtrar por estado">
          {filters.map(option => (
            <button
              key={option.value}
              type="button"
              className={filter === option.value ? 'filter-tab active' : 'filter-tab'}
              aria-pressed={filter === option.value}
              onClick={() => setFilter(option.value)}
            >
              {option.label}
            </button>
          ))}
        </div>
        <label className="search-field">
          <span className="sr-only">Buscar pedidos</span>
          <span aria-hidden="true">⌕</span>
          <input
            value={search}
            onChange={event => setSearch(event.target.value)}
            placeholder="Buscar cliente, SKU o ID"
            type="search"
          />
        </label>
      </div>

      {error && (
        <div className="notice error" role="alert">{error}</div>
      )}

      <div className="orders-scroll-area" tabIndex={0} role="region" aria-label="Lista de pedidos">
        {loading ? (
          <div className="empty-state"><div className="loading-ring" /> Cargando pedidos...</div>
        ) : visible.length === 0 ? (
          <div className="empty-state">
            <div className="empty-icon" aria-hidden="true">▣</div>
            <strong>{orders.length === 0 ? 'Todavía no hay pedidos' : 'No encontramos coincidencias'}</strong>
            <p>{orders.length === 0
              ? 'Crea un pedido para comenzar el seguimiento.'
              : 'Cambia el filtro o el término de búsqueda.'}</p>
          </div>
        ) : (
          <div className="table-scroller">
            <table>
              <thead>
                <tr>
                  <th scope="col">Pedido</th>
                  <th scope="col">Cliente</th>
                  <th scope="col">SKU / Cantidad</th>
                  <th scope="col">Creado</th>
                  <th scope="col">Estado</th>
                </tr>
              </thead>
              <tbody>
                {visible.map(order => (
                  <tr key={order.id}>
                    <td data-label="Pedido" className="mono order-id" title={order.id}>
                      {order.id.slice(0, 8).toUpperCase()}
                    </td>
                    <td data-label="Cliente" className="customer-name">{order.clienteNombre}</td>
                    <td data-label="SKU / Cantidad">
                      <span className="sku-code">{order.sku}</span>
                      <span className="quantity-note">× {order.cantidad}</span>
                    </td>
                    <td data-label="Creado" className="date-cell">{formatDate(order.creadoEn)}</td>
                    <td data-label="Estado">
                      <StatusBadge status={order.estado} />
                      {order.estado === 'Rejected' && order.motivoRechazo && (
                        <span className="reason" title={order.motivoRechazo}>{order.motivoRechazo}</span>
                      )}
                      {order.estado === 'Rejected' && (
                        <button type="button" className="retry-order-button"
                          aria-label={'Preparar un pedido nuevo basado en ' + order.id}
                          onClick={() => onRepeat(order)}>
                          Crear nuevo pedido
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
      <footer className="panel-footer">
        <span>{visible.length} de {orders.length} pedidos</span>
        <span>Actualización automática cada 5 segundos</span>
      </footer>
    </section>
  );
}
