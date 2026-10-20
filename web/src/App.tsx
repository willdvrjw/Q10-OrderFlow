import { useCallback, useEffect, useState } from 'react';
import { OrderForm } from './components/OrderForm';
import { OrdersList } from './components/OrdersList';
import { useOrdersLive } from './hooks/useOrdersLive';
import { getProducts } from './lib/api';
import type { Product } from './types';

interface MetricProps {
  label: string;
  value: number;
  type: string;
  detail: string;
}

function Metric({ label, value, type, detail }: MetricProps) {
  return (
    <article className={'metric-card ' + type}>
      <div className="metric-head">
        <span>{label}</span>
        <span className="metric-symbol" aria-hidden="true">
          {type === 'pending' ? '◷' : type === 'confirmed' ? '✓' : type === 'rejected' ? '×' : '↗'}
        </span>
      </div>
      <strong className="metric-value">{value}</strong>
      <span className="metric-detail">{detail}</span>
    </article>
  );
}

/** Orquestación exclusiva de la presentación: la lógica de negocio permanece en .NET. */
export default function App() {
  const { orders, loading, error, connected, refresh } = useOrdersLive();
  const [products, setProducts] = useState<Product[]>([]);
  const [catalogError, setCatalogError] = useState<string | null>(null);

  const loadCatalog = useCallback(async () => {
    try {
      const loaded = await getProducts();
      setProducts(loaded);
      setCatalogError(null);
    } catch {
      setCatalogError('No se pudo cargar el catálogo de productos.');
    }
  }, []);

  useEffect(() => { void loadCatalog(); }, [loadCatalog]);

  const pending = orders.filter(order => order.estado === 'Pending').length;
  const confirmed = orders.filter(order => order.estado === 'Confirmed').length;
  const rejected = orders.filter(order => order.estado === 'Rejected').length;

  return (
    <div className="site">
      <div className="ambient-shape" aria-hidden="true" />
      <header className="topbar">
        <div className="container header-inner">
          <div className="brand">
            <div className="brand-icon" aria-hidden="true">
              <svg viewBox="0 0 36 36" fill="none">
                <rect x="4" y="4" width="28" height="28" rx="9" stroke="currentColor" strokeWidth="2.5" />
                <path d="M12 20h6l4-7 4 7" stroke="currentColor" strokeWidth="2.7" strokeLinecap="round" strokeLinejoin="round" />
                <circle cx="12" cy="13" r="2" fill="currentColor" />
              </svg>
            </div>
            <div>
              <span className="brand-title">orderflow<span className="brand-dot">.</span></span>
              <span className="brand-subtitle">OPERATIONS CONSOLE</span>
            </div>
          </div>
          <div className={connected ? 'connection live' : 'connection polling'} role="status">
            <span className="connection-dot" />
            {connected ? 'Conectado en vivo' : 'Sincronización periódica'}
          </div>
        </div>
      </header>

      <main className="container main-content">
        <div className="hero">
          <div className="hero-copy">
            <div className="hero-kicker"><span className="sparkle" aria-hidden="true">✦</span> CONTROL DE OPERACIONES</div>
            <h1>Pedidos bajo control<span>.</span></h1>
            <p>Registra solicitudes, monitorea reservas de inventario y acompaña cada pedido hasta su resolución.</p>
          </div>
          <div className="hero-illustration" aria-hidden="true">
            <div className="hero-tile"><span>OF</span><div className="hero-lines"><i /><i /><i /></div></div>
            <span className="floating-chip">● EVENTOS EN VIVO</span>
          </div>
        </div>

        <section className="metrics" aria-label="Resumen de pedidos">
          <Metric label="Pedidos totales" value={orders.length} type="total" detail="Solicitudes registradas" />
          <Metric label="Pendientes" value={pending} type="pending" detail="Esperando inventario" />
          <Metric label="Confirmados" value={confirmed} type="confirmed" detail="Stock reservado" />
          <Metric label="Rechazados" value={rejected} type="rejected" detail="Sin stock disponible" />
        </section>

        <div className="dashboard">
          <OrderForm
            products={products}
            catalogError={catalogError}
            onReloadCatalog={() => void loadCatalog()}
            onCreated={() => refresh(true)}
          />
          <OrdersList
            orders={orders}
            loading={loading}
            error={error}
            onRefresh={() => void refresh(false)}
          />
        </div>

        <footer className="site-footer">
          <span>ORDERFLOW / SERVICIOS DISTRIBUIDOS</span>
          <span>ASP.NET Core · RabbitMQ · PostgreSQL · React</span>
        </footer>
      </main>
    </div>
  );
}
