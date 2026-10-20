import { useState } from 'react';
import type { FormEvent } from 'react';
import { ApiError, createOrder } from '../lib/api';
import type { FormErrors, Product } from '../types';

interface Props {
  products: Product[];
  catalogError: string | null;
  onReloadCatalog: () => void;
  onCreated: () => Promise<void>;
}

/**
 * Formulario controlado: detecta problemas antes de hacer POST.
 * El backend vuelve a validar (nunca confiar solo en el navegador).
 * No se espera a confirmar inventario: el resultado inicial es Pending.
 */
export function OrderForm({ products, catalogError, onReloadCatalog, onCreated }: Props) {
  const [name, setName] = useState('');
  const [sku, setSku] = useState('');
  const [quantity, setQuantity] = useState('1');
  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState<FormErrors>({});
  const [message, setMessage] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const fieldError = (field: string) => errors[field]?.[0];

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;

    const nextErrors: FormErrors = {};
    const cleanedName = name.trim();
    const amount = Number(quantity);

    if (!cleanedName || cleanedName.length > 160) {
      nextErrors.clienteNombre = ['Escribe un nombre válido de hasta 160 caracteres.'];
    }
    if (!products.some(product => product.sku === sku)) {
      nextErrors.sku = ['Selecciona un producto del catálogo.'];
    }
    if (!Number.isInteger(amount) || amount < 1 || amount > 100) {
      nextErrors.cantidad = ['Ingresa una cantidad entera entre 1 y 100.'];
    }

    setErrors(nextErrors);
    setMessage(null);
    setSuccess(false);
    if (Object.keys(nextErrors).length > 0) return;

    setBusy(true);
    try {
      const created = await createOrder({
        clienteNombre: cleanedName,
        sku,
        cantidad: amount,
      });

      setSuccess(true);
      setMessage('Pedido ' + created.id.slice(0, 8).toUpperCase() + ' registrado. Estado inicial: pendiente de inventario.');
      setName('');
      setQuantity('1');
      setErrors({});
      await onCreated();
    } catch (error) {
      setSuccess(false);
      if (error instanceof ApiError) {
        setErrors(error.errors);
        setMessage(error.message);
      } else {
        setMessage('No fue posible comunicarse con Orders API. Revisa la conexión e intenta nuevamente.');
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="panel form-panel" aria-labelledby="new-order-title">
      <div className="panel-heading">
        <div>
          <span className="eyebrow">REGISTRO MANUAL</span>
          <h2 id="new-order-title">Nuevo pedido</h2>
          <p>Registra una solicitud para reservar inventario.</p>
        </div>
        <span className="step-index">01</span>
      </div>

      <form onSubmit={submit} noValidate>
        <label htmlFor="customer">Nombre del cliente <span aria-hidden="true">*</span></label>
        <input
          id="customer"
          name="clienteNombre"
          placeholder="Ej. Laura Martínez"
          autoComplete="name"
          maxLength={160}
          value={name}
          onChange={event => setName(event.target.value)}
          aria-invalid={Boolean(fieldError('clienteNombre'))}
          aria-describedby={fieldError('clienteNombre') ? 'customer-error' : undefined}
        />
        {fieldError('clienteNombre') && (
          <small id="customer-error" className="field-error">{fieldError('clienteNombre')}</small>
        )}

        <label htmlFor="product">Producto / SKU <span aria-hidden="true">*</span></label>
        <select
          id="product"
          name="sku"
          value={sku}
          onChange={event => setSku(event.target.value)}
          disabled={products.length === 0}
          aria-invalid={Boolean(fieldError('sku'))}
          aria-describedby={fieldError('sku') ? 'product-error' : undefined}
        >
          <option value="">Selecciona un producto...</option>
          {products.map(product => (
            <option key={product.sku} value={product.sku}>
              {product.nombre} · {product.sku}
            </option>
          ))}
        </select>
        {fieldError('sku') && (
          <small id="product-error" className="field-error">{fieldError('sku')}</small>
        )}

        {catalogError && (
          <div className="notice error" role="alert">
            {catalogError}
            <button type="button" className="inline-action" onClick={onReloadCatalog}>Reintentar</button>
          </div>
        )}

        <label htmlFor="quantity">Cantidad <span aria-hidden="true">*</span></label>
        <input
          id="quantity"
          name="cantidad"
          type="number"
          min="1"
          max="100"
          step="1"
          inputMode="numeric"
          value={quantity}
          onChange={event => setQuantity(event.target.value)}
          aria-invalid={Boolean(fieldError('cantidad'))}
          aria-describedby={fieldError('cantidad') ? 'quantity-error' : 'quantity-help'}
        />
        {fieldError('cantidad') ? (
          <small id="quantity-error" className="field-error">{fieldError('cantidad')}</small>
        ) : (
          <small id="quantity-help" className="field-hint">De 1 a 100 unidades por pedido.</small>
        )}

        <div className="form-rule" />
        <div className="form-footer">
          <p>El pedido se registra como <strong>Pending</strong>. La confirmación llegará después de procesar el inventario.</p>
          <button className="primary-button" type="submit" disabled={busy || products.length === 0}>
            {busy ? 'Registrando...' : 'Crear pedido'}
            {!busy && <span aria-hidden="true">↗</span>}
          </button>
        </div>

        {message && (
          <div className={success ? 'notice success' : 'notice error'} role="status" aria-live="polite">
            {message}
          </div>
        )}
      </form>
    </section>
  );
}
