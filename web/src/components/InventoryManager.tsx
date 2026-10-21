import { useCallback, useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { ApiError, adjustInventory, getInventory } from '../lib/api';
import type { InventoryOperation, InventoryStock, Product } from '../types';

interface Props {
  products: Product[];
}

type Feedback = { kind: 'success' | 'error'; text: string } | null;

/**
 * Permite consultar, sumar o establecer existencias sin acceder a Orders DB.
 * El servidor exige el saldo leído: un ajuste obsoleto recibe 409 y no pisa
 * una reserva concurrente. Es una consola administrativa de demostración.
 */
export function InventoryManager({ products }: Props) {
  const [stock, setStock] = useState<InventoryStock[]>([]);
  const [selectedSku, setSelectedSku] = useState('');
  const [operation, setOperation] = useState<InventoryOperation>('agregar');
  const [amount, setAmount] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      const current = await getInventory();
      setStock(current);
      setLoadError(null);
    } catch {
      setLoadError('No fue posible consultar el inventario. Reintenta la consulta.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
    const timer = window.setInterval(() => void refresh(), 10000);
    return () => window.clearInterval(timer);
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
    requestedAmount <= 1000000;
  const projected = selected && validAmount
    ? operation === 'agregar' ? selected.disponible + requestedAmount : requestedAmount
    : null;

  const productName = (sku: string) =>
    products.find(product => product.sku === sku)?.nombre ?? 'Producto ' + sku;

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving || !selected) return;
    if (!validAmount || projected === null || projected > 2147483647) {
      setFeedback({ kind: 'error', text: 'Introduce una cantidad entera válida para realizar el ajuste.' });
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
        text: 'Existencias actualizadas para ' + selected.sku + ': ' +
          result.disponible.toLocaleString('es-CO') + ' unidades disponibles.',
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
          <p>Consulta las cantidades actuales y registra nuevas existencias sin modificar los pedidos anteriores.</p>
        </div>
        <button className="refresh-button" type="button" onClick={() => void refresh()} aria-label="Actualizar inventario">
          <span aria-hidden="true">↻</span> Actualizar
        </button>
      </div>

      {loadError && <div className="notice error inventory-error" role="alert">{loadError}</div>}
      <div className="inventory-layout">
        <div className="stock-list" aria-label="Existencias por producto">
          {loading ? (
            <div className="inventory-loading">Consultando inventario...</div>
          ) : stock.length === 0 ? (
            <div className="inventory-loading">No se encontraron productos con existencias registradas.</div>
          ) : stock.map(item => (
            <button
              className={'stock-card' + (selectedSku === item.sku ? ' selected' : '')}
              key={item.sku}
              type="button"
              onClick={() => { setSelectedSku(item.sku); setFeedback(null); }}
              aria-pressed={selectedSku === item.sku}
            >
              <span className="stock-card-name">{productName(item.sku)}</span>
              <span className="stock-card-sku">{item.sku}</span>
              <span className="stock-card-bottom">
                <strong>{item.disponible.toLocaleString('es-CO')}</strong>
                <span>unidades disponibles</span>
              </span>
            </button>
          ))}
          <p className="stock-note">Las existencias se actualizan automáticamente cada 10 segundos.</p>
        </div>

        <form className="inventory-editor" onSubmit={event => void submit(event)} noValidate>
          <h3>Actualizar existencias</h3>
          <p>Selecciona un producto y registra un ingreso o define su cantidad total.</p>
          <label htmlFor="inventory-product">Producto</label>
          <select
            id="inventory-product"
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
            <span>Después del ajuste <strong>{projected !== null && projected <= 2147483647 ? projected.toLocaleString('es-CO') : '—'}</strong></span>
          </div>
          {operation === 'establecer' && (
            <p className="inventory-warning">Esta opción reemplaza el total actual. Comprueba las existencias antes de guardar.</p>
          )}
          <button className="primary-button" type="submit"
            disabled={saving || !selected || !validAmount || projected === null || projected > 2147483647}>
            {saving ? 'Guardando ajuste...' : 'Guardar existencias'}
          </button>
          {feedback && (
            <div className={'notice ' + feedback.kind} role="status" aria-live="polite">{feedback.text}</div>
          )}
          <p className="inventory-disclaimer">Los ajustes modifican únicamente el stock actual del entorno de prueba.</p>
        </form>
      </div>
    </section>
  );
}
