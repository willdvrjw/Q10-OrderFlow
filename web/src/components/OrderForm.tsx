import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { ApiError, createOrder, getInventory, getOrder } from '../lib/api';
import type { FormErrors, InventoryStock, Order, Product } from '../types';

export interface RepeatOrderPrefill {
  sourceOrderId: string;
  clienteNombre: string;
  sku: string;
  cantidad: number;
}

interface Props {
  products: Product[];
  prefill: RepeatOrderPrefill | null;
  catalogError: string | null;
  onReloadCatalog: () => void;
  onCreated: () => Promise<void>;
  onCancelRepeat: () => void;
}

/**
 * Formulario controlado: detecta problemas antes de hacer POST.
 * El backend vuelve a validar (nunca confiar solo en el navegador).
 * No se espera a confirmar inventario: el resultado inicial es Pending.
 */
export function OrderForm({
  products, prefill, catalogError, onReloadCatalog, onCreated, onCancelRepeat,
}: Props) {
  const [name, setName] = useState('');
  const [sku, setSku] = useState('');
  const [quantity, setQuantity] = useState('1');
  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState<FormErrors>({});
  const [message, setMessage] = useState<string | null>(null);
  const [messageTone, setMessageTone] = useState<'success' | 'error' | 'info'>('info');

  useEffect(() => {
    if (!prefill) return;
    // Solo se copian los campos; nada se envía hasta que el usuario confirme.
    setName(prefill.clienteNombre);
    setSku(prefill.sku);
    setQuantity(String(prefill.cantidad));
    setErrors({});
    setMessageTone('info');
    setMessage(
      'Repetición del pedido ' + prefill.sourceOrderId.slice(0, 8).toUpperCase() +
      ': conserva exactamente ' + prefill.cantidad + ' unidades de ' + prefill.sku +
      '. Se volverá a comprobar el pedido original y el stock antes de guardar.'
    );
  }, [prefill]);

  const fieldError = (field: string) => errors[field]?.[0];
  const repeatQuantityMismatch = prefill !== null &&
    (quantity.trim() === '' || !Number.isInteger(Number(quantity)) ||
      Number(quantity) !== prefill.cantidad);

  function cancelRepeat() {
    onCancelRepeat();
    setName('');
    setSku('');
    setQuantity('1');
    setErrors({});
    setMessage(null);
    setMessageTone('info');
  }

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
    if (quantity.trim() === '' || !Number.isInteger(amount) || amount < 1 || amount > 100) {
      nextErrors.cantidad = ['Ingresa una cantidad entera entre 1 y 100.'];
    }

    // Repetir un rechazo no permite cambiar los datos del pedido de origen.
    // Otro cliente, SKU o cantidad requieren salir de este modo y crear
    // una solicitud manual independiente.
    if (prefill !== null) {
      if (cleanedName !== prefill.clienteNombre) {
        nextErrors.clienteNombre = ['Debes conservar el cliente del pedido rechazado.'];
      }
      if (sku !== prefill.sku) {
        nextErrors.sku = ['Debes conservar el producto del pedido rechazado.'];
      }
      if (amount !== prefill.cantidad || quantity.trim() === '') {
        nextErrors.cantidad = [
          'El pedido rechazado contiene ' + prefill.cantidad +
          ' unidades. No puedes cambiar esa cantidad durante una repetición.'
        ];
      }
    }

    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      if (prefill !== null) {
        setMessageTone('error');
        setMessage('No se creó un pedido. Para solicitar otra cantidad, cancela la repetición y utiliza el registro normal.');
      }
      return;
    }
    setMessage(null);

    setBusy(true);
    try {
      if (prefill !== null) {
        // La disponibilidad vista en la tabla puede haber cambiado por otra
        // reserva; no crear una repetición sin una comprobación actualizada.
        let original: Order;
        let latest: InventoryStock[];
        try {
          [original, latest] = await Promise.all([
            getOrder(prefill.sourceOrderId),
            getInventory(),
          ]);
        } catch {
          setMessageTone('error');
          setMessage('No fue posible comprobar el pedido original y las existencias. No se creó ningún pedido.');
          return;
        }

        if (original.estado !== 'Rejected' ||
            original.clienteNombre !== cleanedName ||
            original.sku !== sku ||
            original.cantidad !== amount ||
            original.clienteNombre !== prefill.clienteNombre ||
            original.sku !== prefill.sku ||
            original.cantidad !== prefill.cantidad) {
          setErrors({
            cantidad: [
              'La repetición debe conservar las ' + original.cantidad +
              ' unidades del pedido rechazado, sin cambiar el cliente ni el producto.'
            ]
          });
          setMessageTone('error');
          setMessage('Los datos no coinciden con el pedido registrado. No se creó ninguna solicitud nueva.');
          return;
        }

        const available = latest.find(item => item.sku === sku)?.disponible ?? 0;
        if (available < amount) {
          setErrors({ cantidad: [
            'Solo hay ' + available + ' unidades disponibles y se solicitaron ' + amount + '.'
          ] });
          setMessageTone('error');
          setMessage(
            'No se creó un pedido nuevo. Repón las existencias y vuelve a verificar antes de guardar.'
          );
          return;
        }
      }

      const created = await createOrder({
        clienteNombre: cleanedName,
        sku,
        cantidad: amount,
      });

      setMessageTone('success');
      setMessage('Pedido ' + created.id.slice(0, 8).toUpperCase() + ' registrado. Estado inicial: pendiente de inventario.');
      setName('');
      setSku('');
      setQuantity('1');
      setErrors({});
      await onCreated();
    } catch (error) {
      setMessageTone('error');
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
          <span className="eyebrow">{prefill ? 'REPETICIÓN CONTROLADA' : 'REGISTRO MANUAL'}</span>
          <h2 id="new-order-title">{prefill ? 'Repetir pedido rechazado' : 'Nuevo pedido'}</h2>
          <p>{prefill
            ? 'La repetición debe conservar los datos originales y comprobar las existencias.'
            : 'Registra una solicitud para reservar inventario.'}</p>
        </div>
      </div>

      <form onSubmit={submit} noValidate>
        {prefill && (
          <div className="repeat-origin" role="note">
            <p>
              <strong>Pedido de origen: {prefill.sourceOrderId.slice(0, 8).toUpperCase()}</strong>
              <span> · Cantidad registrada: {prefill.cantidad} unidades</span>
            </p>
            <p>Para cambiar de cliente, producto o cantidad, cancela esta repetición y utiliza un pedido normal.</p>
            <button type="button" className="cancel-repeat" onClick={cancelRepeat} disabled={busy}>
              Cancelar repetición
            </button>
          </div>
        )}
        <label htmlFor="customer">Nombre del cliente <span aria-hidden="true">*</span></label>
        <input
          id="customer"
          name="clienteNombre"
          placeholder="Ej. Laura Martínez"
          autoComplete="name"
          maxLength={160}
          value={name}
          readOnly={prefill !== null}
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
          disabled={products.length === 0 || prefill !== null}
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
          onChange={event => {
            setQuantity(event.target.value);
            setErrors(previous => {
              const next = { ...previous };
              delete next.cantidad;
              return next;
            });
            setMessage(null);
          }}
          aria-invalid={Boolean(fieldError('cantidad') || repeatQuantityMismatch)}
          aria-describedby={repeatQuantityMismatch ? 'repeat-quantity-error' :
            fieldError('cantidad') ? 'quantity-error' : 'quantity-help'}
        />
        {repeatQuantityMismatch && prefill ? (
          <small id="repeat-quantity-error" className="field-error" role="alert">
            La cantidad original es {prefill.cantidad}, no {quantity || 'ninguna'}.
            No se permite crear otra cantidad desde este pedido rechazado.
          </small>
        ) : fieldError('cantidad') ? (
          <small id="quantity-error" className="field-error">{fieldError('cantidad')}</small>
        ) : (
          <small id="quantity-help" className="field-hint">
            {prefill ? 'Cantidad original obligatoria: ' + prefill.cantidad + ' unidades.' :
              'De 1 a 100 unidades por pedido.'}
          </small>
        )}

        <div className="form-rule" />
        <div className="form-footer">
          <p>El pedido se registra como <strong>pendiente</strong>. Se confirmará después de reservar inventario.</p>
          <button className="primary-button" type="submit"
            disabled={busy || products.length === 0 || repeatQuantityMismatch}>
            {busy ? 'Registrando...' : prefill ? 'Confirmar repetición' : 'Crear pedido'}
            {!busy && <span aria-hidden="true">↗</span>}
          </button>
        </div>

        {message && (
          <div className={'notice ' + messageTone} role="status" aria-live="polite">
            {message}
          </div>
        )}
      </form>
    </section>
  );
}
