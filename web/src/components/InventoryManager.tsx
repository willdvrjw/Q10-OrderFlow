import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { useCallback, useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { ApiError, adjustInventory, getInventory } from '../lib/api';
import { FlashNotice } from './FlashNotice';
import { NewProductForm } from './NewProductForm';
import type { InventoryOperation, InventoryStock, Product } from '../types';

interface Props {
  products: Product[];
  onStockUpdate: (stock: InventoryStock[]) => void;
  onStockUnavailable: () => void;
  onProductCreated: () => Promise<void>;
}
type Feedback = { kind: 'success' | 'error'; text: string } | null;

function stockStatus(available: number) {
  if (available === 0) return { label: 'Agotado', kind: 'empty' };
  if (available <= 5) return { label: 'Existencias bajas', kind: 'low' };
  return { label: 'Disponible', kind: 'available' };
}

/**
 * Únicamente Inventory gestiona existencias. SignalR avisa tras cada reserva
 * o ajuste confirmado; GET /inventory sigue siendo la fuente de verdad.
 * El polling mantiene actualizado el panel si se pierde la conexión en vivo.
 */
export function InventoryManager({ products, onStockUpdate, onStockUnavailable, onProductCreated }: Props) {
  const [stock, setStock] = useState<InventoryStock[]>([]);
  const [selectedSku, setSelectedSku] = useState('');
  const [operation, setOperation] = useState<InventoryOperation>('agregar');
  const [amount, setAmount] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [connected, setConnected] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const lastRequest = useRef(0);
  const editorRef = useRef<HTMLFormElement>(null);
  const stockScrollRef = useRef<HTMLDivElement>(null);
  const [editorHeight, setEditorHeight] = useState<number>();
  const [showCreate, setShowCreate] = useState(false);

  // La altura de la columna de productos sigue a la del editor; solamente
  // la lista izquierda se desplaza cuando aumentan los SKU.
  useEffect(() => {
    const editor = editorRef.current;
    if (!editor) return;
    const observer = new ResizeObserver(() => {
      setEditorHeight(Math.ceil(editor.getBoundingClientRect().height));
    });
    observer.observe(editor);
    return () => observer.disconnect();
  }, []);

  const refresh = useCallback(async () => {
    const requestId = ++lastRequest.current;
    try {
      const current = await getInventory();
      if (requestId !== lastRequest.current) return;
      setStock(current);
      onStockUpdate(current);
      setLoadError(null);
    } catch {
      if (requestId === lastRequest.current) {
        onStockUnavailable();
        setLoadError('No fue posible consultar el inventario. Reintenta la consulta.');
      }
    } finally {
      if (requestId === lastRequest.current) setLoading(false);
    }
  }, [onStockUpdate, onStockUnavailable]);

  useEffect(() => {
    let disposed = false;
    let connecting = false;
    void refresh();

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/inventory')
      .withAutomaticReconnect([0, 1500, 4000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('inventoryChanged', () => {
      if (!disposed) void refresh();
    });
    connection.onreconnecting(() => {
      if (!disposed) setConnected(false);
    });
    connection.onreconnected(() => {
      if (!disposed) {
        setConnected(true);
        void refresh();
      }
    });
    connection.onclose(() => {
      if (!disposed) setConnected(false);
    });

    const start = async () => {
      if (disposed || connecting || connection.state !== HubConnectionState.Disconnected) return;
      connecting = true;
      try {
        await connection.start();
        if (!disposed) {
          setConnected(true);
          void refresh();
        }
      } catch {
        if (!disposed) setConnected(false);
      } finally {
        connecting = false;
      }
    };
    void start();

    // También reintenta el inicio fallido de SignalR, además de consultar stock.
    const interval = window.setInterval(() => {
      if (disposed) return;
      void refresh();
      void start();
    }, 10000);
    return () => {
      disposed = true;
      ++lastRequest.current;
      window.clearInterval(interval);
      void connection.stop();
    };
  }, [refresh]);

  useEffect(() => {
    if (stock.length && !stock.some(item => item.sku === selectedSku)) {
      setSelectedSku(stock[0].sku);
    }
  }, [stock, selectedSku]);

  const selected = stock.find(item => item.sku === selectedSku);
  const requestedAmount = Number(amount);
  const validAmount = amount.trim() !== '' &&
    Number.isInteger(requestedAmount) &&
    requestedAmount >= (operation === 'agregar' ? 1 : 0) &&
    requestedAmount <= 1_000_000;
  const projected = selected && validAmount
    ? operation === 'agregar' ? selected.disponible + requestedAmount : requestedAmount
    : null;

  const productName = (sku: string) =>
    products.find(product => product.sku === sku)?.nombre ?? 'Producto ' + sku;

  async function handleProductCreated(sku: string, nombre: string) {
    // Refrescar tanto el catálogo de Orders (para nuevos pedidos) como
    // el inventario del Worker; mostrar y seleccionar el nuevo SKU.
    await onProductCreated();
    await refresh();
    setSelectedSku(sku);
    setShowCreate(false);
    setFeedback({
      kind: 'success',
      text: 'Producto ' + nombre + ' (' + sku +
        ') registrado. Ya está disponible para crear pedidos y administrar existencias.',
    });
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving || !selected) return;
    if (!validAmount || projected === null || projected > 2_147_483_647) {
      setFeedback({ kind: 'error', text: 'Introduce una cantidad entera válida para el ajuste.' });
      return;
    }

    setSaving(true);
    setFeedback(null);
    try {
      const result = await adjustInventory({
        sku: selected.sku,
        operacion: operation,
        cantidad: requestedAmount,
        existenciaEsperada: selected.disponible,
      });
      setFeedback({
        kind: 'success',
        text: 'Existencias actualizadas: ' +
          result.disponible.toLocaleString('es-CO') + ' unidades disponibles de ' + selected.sku + '.',
      });
      setAmount('');
      await refresh();
    } catch (error) {
      setFeedback({
        kind: 'error',
        text: error instanceof ApiError
          ? error.message
          : 'No fue posible guardar el ajuste. Comprueba la conexión.',
      });
      await refresh();
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="panel inventory-panel" id="inventario" aria-labelledby="inventory-title">
      <div className="panel-heading inventory-heading">
        <div>
          <span className="eyebrow">CONTROL DE EXISTENCIAS</span>
          <h2 id="inventory-title">Inventario disponible</h2>
          <p>Consulta existencias y registra ingresos o ajustes. Las reservas de pedidos se reflejan automáticamente.</p>
        </div>
        <div className="inventory-heading-actions">
          <span className={'inventory-sync' + (connected ? ' live' : '')} role="status">
            <span className="inventory-sync-dot" aria-hidden="true" />
            {connected ? 'En tiempo real' : 'Actualización periódica'}
          </span>
          <button className="refresh-button" type="button" onClick={() => void refresh()} aria-label="Actualizar inventario">
            <span aria-hidden="true">↻</span> Actualizar
          </button>
        </div>
      </div>

      {loadError && (
        <FlashNotice tone="error" className="inventory-error"
          onClose={() => setLoadError(null)}>
          {loadError}
        </FlashNotice>
      )}
      <div className="inventory-layout">
        <div className="stock-column" style={{ height: editorHeight ? editorHeight + 'px' : undefined }}>
          <div ref={stockScrollRef} className="stock-scroll-area" role="region"
            aria-label="Catálogo de productos, desplazamiento independiente" tabIndex={0}>
            {showCreate && (
              <NewProductForm products={products} onCreated={handleProductCreated} />
            )}
            <div className="stock-list" aria-label="Existencias por producto">
          {loading ? (
            <div className="inventory-loading">Consultando inventario...</div>
          ) : stock.length === 0 ? (
            <div className="inventory-loading">No se encontraron productos con existencias registradas.</div>
          ) : stock.map(item => {
            const status = stockStatus(item.disponible);
            return (
              <button
                className={'stock-card' + (selectedSku === item.sku ? ' selected' : '')}
                key={item.sku}
                type="button"
                onClick={() => { setSelectedSku(item.sku); setFeedback(null); }}
                aria-pressed={selectedSku === item.sku}
              >
                <span className="stock-card-heading">
                  <span className="stock-card-name">{productName(item.sku)}</span>
                  <span className="stock-card-sku">{item.sku}</span>
                </span>
                <span className={'stock-card-status ' + status.kind}>{status.label}</span>
                <span className="stock-card-bottom">
                  <strong>{item.disponible.toLocaleString('es-CO')}</strong>
                  <span>unidades disponibles</span>
                </span>
              </button>
            );
          })}
            </div>
          </div>
          <div className="stock-column-actions">
            <button
              type="button"
              className="stock-add-button"
              aria-expanded={showCreate}
              onClick={() => {
                setShowCreate(value => !value);
                stockScrollRef.current?.scrollTo({ top: 0, behavior: 'smooth' });
              }}
            >
              <span aria-hidden="true">{showCreate ? '×' : '+'}</span>
              {showCreate ? 'Cancelar alta de producto' : 'Agregar producto nuevo'}
            </button>
            <p className="stock-note">
              Las alertas de existencias bajas son informativas (5 unidades o menos).
              No modifican la reserva de pedidos.
            </p>
          </div>
        </div>

        <form ref={editorRef} className="inventory-editor" onSubmit={event => void submit(event)} noValidate>
          <h3>Actualizar existencias</h3>
          <p>Selecciona un producto y registra un ingreso o define su cantidad total.</p>
          <label htmlFor="inventory-product">Producto</label>
          <select
            id="inventory-product"
            name="sku"
            value={selectedSku}
            onChange={event => { setSelectedSku(event.target.value); setFeedback(null); }}
            disabled={saving || stock.length === 0}
          >
            {stock.map(item => (
              <option key={item.sku} value={item.sku}>{productName(item.sku)} · {item.sku}</option>
            ))}
          </select>
          <label htmlFor="inventory-action">Tipo de ajuste</label>
          <select
            id="inventory-action"
            name="operacion"
            value={operation}
            onChange={event => { setOperation(event.target.value as InventoryOperation); setFeedback(null); }}
            disabled={saving}
          >
            <option value="agregar">Agregar unidades</option>
            <option value="establecer">Establecer cantidad total</option>
          </select>
          <label htmlFor="inventory-amount">
            {operation === 'agregar' ? 'Unidades que ingresan' : 'Nueva cantidad disponible'}
          </label>
          <input
            id="inventory-amount"
            name="cantidad"
            type="number"
            min={operation === 'agregar' ? '1' : '0'}
            max="1000000"
            step="1"
            inputMode="numeric"
            placeholder={operation === 'agregar' ? 'Ej. 25' : 'Ej. 150'}
            value={amount}
            onChange={event => setAmount(event.target.value)}
            disabled={saving}
            aria-invalid={amount !== '' && !validAmount}
          />
          <div className="inventory-preview" aria-live="polite">
            <span>Disponibles ahora <strong>{selected?.disponible.toLocaleString('es-CO') ?? '—'}</strong></span>
            <span>Después del ajuste <strong>{projected !== null && projected <= 2_147_483_647 ? projected.toLocaleString('es-CO') : '—'}</strong></span>
          </div>
          {operation === 'establecer' && (
            <p className="inventory-warning">Esta opción reemplaza el total actual. Comprueba las existencias antes de guardar.</p>
          )}
          <button className="primary-button" type="submit"
            disabled={saving || !selected || !validAmount || projected === null || projected > 2_147_483_647}>
            {saving ? 'Guardando ajuste...' : 'Guardar existencias'}
          </button>
          {feedback && (
            <FlashNotice tone={feedback.kind} onClose={() => setFeedback(null)}>
              {feedback.text}
            </FlashNotice>
          )}
          <p className="inventory-disclaimer">Las existencias actualizadas se utilizarán para las reservas de pedidos nuevos o todavía pendientes de procesamiento.</p>
        </form>
      </div>
    </section>
  );
}
