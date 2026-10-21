import { useCallback, useEffect, useRef, useState } from 'react';
import { OrderForm } from './components/OrderForm';
import type { RepeatOrderPrefill } from './components/OrderForm';
import { OrdersList } from './components/OrdersList';
import { InventoryManager } from './components/InventoryManager';
import { useOrdersLive } from './hooks/useOrdersLive';
import { getProducts } from './lib/api';
import type { Order, Product } from './types';

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
  const formPanelRef = useRef<HTMLDivElement>(null);
  const [formPanelHeight, setFormPanelHeight] = useState<number>();
  const [repeatPrefill, setRepeatPrefill] = useState<RepeatOrderPrefill | null>(null);

  const prepareNewOrder = useCallback((order: Order) => {
    // No se reabre ni modifica el pedido rechazado. Solo se precarga otro.
    setRepeatPrefill({
      clienteNombre: order.clienteNombre,
      sku: order.sku,
      cantidad: order.cantidad,
    });
    formPanelRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, []);

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

  // Igualar la altura de seguimiento con la del formulario incluso si cambian
  // sus mensajes de validación. La tabla usa scroll independiente.
  useEffect(() => {
    const element = formPanelRef.current;
    if (!element) return;
    const observer = new ResizeObserver(() => {
      setFormPanelHeight(Math.ceil(element.getBoundingClientRect().height));
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

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
              <span className="brand-title">OrderFlow</span>
            </div>
          </div>
          <nav className="topnav" aria-label="Secciones">
            <a href="#pedidos">Pedidos</a>
            <a href="#inventario">Inventario</a>
          </nav>
          <div className={connected ? 'connection live' : 'connection polling'} role="status">
            <span className="connection-dot" />
            {connected ? 'Conectado en vivo' : 'Sincronización periódica'}
          </div>
        </div>
      </header>

      <main className="container main-content">
        <section className="hero" aria-labelledby="hero-title">
          <div className="hero-copy">
            <span className="hero-kicker">CONTROL DE OPERACIONES</span>
            <h1 id="hero-title">Pedidos bajo control</h1>
            <p>Registra solicitudes, monitorea reservas de inventario y acompaña cada pedido hasta su resolución.</p>
          </div>
        </section>

        <section className="metrics" aria-label="Resumen de pedidos">
          <Metric label="Pedidos totales" value={orders.length} type="total" detail="Solicitudes registradas" />
          <Metric label="Pendientes" value={pending} type="pending" detail="Esperando inventario" />
          <Metric label="Confirmados" value={confirmed} type="confirmed" detail="Inventario reservado" />
          <Metric label="Rechazados" value={rejected} type="rejected" detail="Sin existencias" />
        </section>

        <div className="dashboard" id="pedidos">
          <div ref={formPanelRef} className="form-panel-measure">
            <OrderForm
              products={products}
              prefill={repeatPrefill}
              catalogError={catalogError}
              onReloadCatalog={() => void loadCatalog()}
              onCreated={() => refresh(true)}
            />
          </div>
          <OrdersList
            orders={orders}
            loading={loading}
            error={error}
            onRefresh={() => void refresh(false)}
            height={formPanelHeight}
            onRepeat={prepareNewOrder}
          />
        </div>

        <InventoryManager products={products} />

        <footer className="site-footer">
          <span>ORDERFLOW / SERVICIOS DISTRIBUIDOS</span>
          <span>ASP.NET Core · RabbitMQ · PostgreSQL · React</span>
        </footer>
      </main>
    </div>
  );
}
