import { useCallback, useEffect, useRef, useState } from 'react';
import { OrderForm } from './components/OrderForm';
import type { RepeatOrderPrefill } from './components/OrderForm';
import { OrdersList } from './components/OrdersList';
import { InventoryManager } from './components/InventoryManager';
import { useOrdersLive } from './hooks/useOrdersLive';
import { getInventory, getProducts } from './lib/api';
import type { InventoryStock, Order, Product } from './types';

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
  const { orders, loading, error, connected, refresh, dismissError } = useOrdersLive();
  const [products, setProducts] = useState<Product[]>([]);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const formPanelRef = useRef<HTMLDivElement>(null);
  const [formPanelHeight, setFormPanelHeight] = useState<number>();
  const [repeatPrefill, setRepeatPrefill] = useState<RepeatOrderPrefill | null>(null);
  const [inventoryStock, setInventoryStock] = useState<InventoryStock[]>([]);
  const [inventoryReady, setInventoryReady] = useState(false);
  const [repeatCheckingId, setRepeatCheckingId] = useState<string | null>(null);
  const [repeatFeedback, setRepeatFeedback] = useState<string | null>(null);

  // La vista de inventario y los pedidos rechazados comparten los mismos
  // datos recibidos por SignalR y GET /inventory (sin duplicar conexiones).
  const updateInventory = useCallback((stock: InventoryStock[]) => {
    setInventoryStock(stock);
    setInventoryReady(true);
  }, []);
  const markInventoryUnavailable = useCallback(() => {
    setInventoryReady(false);
  }, []);

  const prepareNewOrder = useCallback(async (order: Order) => {
    if (repeatCheckingId !== null || order.estado !== 'Rejected') return;
    setRepeatCheckingId(order.id);
    setRepeatFeedback(null);

    try {
      // No confiar en la última actualización visual: solicitar el saldo actual
      // antes de habilitar la repetición de un pedido rechazado.
      const latestStock = await getInventory();
      updateInventory(latestStock);
      const available = latestStock.find(item => item.sku === order.sku)?.disponible ?? 0;

      if (available < order.cantidad) {
        setRepeatFeedback(
          'No se puede preparar un pedido nuevo de ' + order.sku +
          ': hay ' + available + ' unidades y se necesitan ' + order.cantidad +
          '. Actualiza las existencias antes de intentarlo.'
        );
        return;
      }

      // Rejected sigue terminal. Únicamente se precargan los datos del pedido
      // nuevo; nunca se envía automáticamente ni se altera el pedido original.
      setRepeatPrefill({
        sourceOrderId: order.id,
        clienteNombre: order.clienteNombre,
        sku: order.sku,
        cantidad: order.cantidad,
      });
      formPanelRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    } catch {
      markInventoryUnavailable();
      setRepeatFeedback(
        'No fue posible comprobar las existencias. Intenta actualizar el inventario.'
      );
    } finally {
      setRepeatCheckingId(null);
    }
  }, [repeatCheckingId, updateInventory, markInventoryUnavailable]);

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

  // Si se registra un SKU en otra pestaña, Inventory notifica por SignalR.
  // El catálogo de Orders se sincroniza sin exigir recargar la página.
  useEffect(() => {
    if (inventoryStock.some(item => !products.some(product => product.sku === item.sku)))
      void loadCatalog();
  }, [inventoryStock, products, loadCatalog]);

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
              onCancelRepeat={() => {
                setRepeatPrefill(null);
                setRepeatFeedback(null);
              }}
              catalogError={catalogError}
              onReloadCatalog={() => void loadCatalog()}
              onDismissCatalogError={() => setCatalogError(null)}
              onCreated={async () => {
                setRepeatPrefill(null);
                await refresh(true);
              }}
            />
          </div>
          <OrdersList
            orders={orders}
            loading={loading}
            error={error}
            onRefresh={() => void refresh(false)}
            onDismissError={dismissError}
            onDismissRepeatFeedback={() => setRepeatFeedback(null)}
            height={formPanelHeight}
            stock={inventoryStock}
            stockReady={inventoryReady}
            checkingRepeatId={repeatCheckingId}
            repeatFeedback={repeatFeedback}
            onRepeat={prepareNewOrder}
          />
        </div>

        <InventoryManager
          products={products}
          onStockUpdate={updateInventory}
          onStockUnavailable={markInventoryUnavailable}
          onProductCreated={loadCatalog}
        />

        <footer className="site-footer">
          <span>ORDERFLOW / SERVICIOS DISTRIBUIDOS</span>
          <span>ASP.NET Core · RabbitMQ · PostgreSQL · React</span>
        </footer>
      </main>
    </div>
  );
}
