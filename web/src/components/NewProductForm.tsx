import { useState } from 'react';
import type { FormEvent } from 'react';
import { ApiError, createProduct } from '../lib/api';
import type { FormErrors, Product } from '../types';
import { FlashNotice } from './FlashNotice';

interface Props {
  products: Product[];
  onCreated: (sku: string, nombre: string) => Promise<void>;
}

/** Alta de productos persistentes. La API coordina catálogo e inventario. */
export function NewProductForm({ products, onCreated }: Props) {
  const [name, setName] = useState('');
  const [sku, setSku] = useState('');
  const [initialStock, setInitialStock] = useState('0');
  const [saving, setSaving] = useState(false);
  const [errors, setErrors] = useState<FormErrors>({});
  const [message, setMessage] = useState<string | null>(null);

  const normalizedSku = sku.trim().toUpperCase();
  const validSku = /^[A-Z0-9][A-Z0-9._-]{0,63}$/.test(normalizedSku);
  const stockNumber = Number(initialStock);
  const validStock = initialStock.trim() !== '' &&
    Number.isInteger(stockNumber) && stockNumber >= 0 && stockNumber <= 1_000_000;
  const fieldError = (field: string) => errors[field]?.[0];

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving) return;

    const next: FormErrors = {};
    const cleanedName = name.trim();
    if (cleanedName.length < 1 || cleanedName.length > 160)
      next.nombre = ['Escribe el nombre del producto, máximo 160 caracteres.'];
    if (!validSku)
      next.sku = ['Usa letras, números, puntos, guiones o guiones bajos (máximo 64).'];
    if (products.some(product => product.sku.toUpperCase() === normalizedSku))
      next.sku = ['Ese SKU ya está registrado. Utiliza un identificador diferente.'];
    if (!validStock)
      next.disponible = ['Ingresa un número entero de 0 a 1.000.000.'];

    setErrors(next);
    setMessage(null);
    if (Object.keys(next).length > 0) return;

    setSaving(true);
    try {
      const created = await createProduct({
        sku: normalizedSku,
        nombre: cleanedName,
        disponible: stockNumber,
      });
      // El servidor solo responde 201 después de crear las existencias y el
      // catálogo en sus bases. Recargamos ambos selectores automáticamente.
      await onCreated(created.sku, created.nombre);
    } catch (error) {
      if (error instanceof ApiError) {
        setErrors(error.errors);
        setMessage(error.message);
      } else {
        setMessage('No fue posible completar el alta. Reintenta con el mismo SKU y los mismos datos.');
      }
    } finally {
      setSaving(false);
    }
  }

  return (
    <form className="new-product-form" onSubmit={event => void submit(event)} noValidate>
      <h3>Registrar producto nuevo</h3>
      <p>Define el SKU, su nombre y el inventario inicial. Aparecerá en los pedidos y en las existencias.</p>

      <label htmlFor="new-product-name">Nombre del producto <span aria-hidden="true">*</span></label>
      <input id="new-product-name" name="nombre" type="text"
        maxLength={160} value={name} autoComplete="off" disabled={saving}
        placeholder="Ej. Agenda personalizada"
        aria-invalid={Boolean(fieldError('nombre'))}
        onChange={event => { setName(event.target.value); setErrors({}); setMessage(null); }} />
      {fieldError('nombre') && <small className="field-error">{fieldError('nombre')}</small>}

      <label htmlFor="new-product-sku">SKU único <span aria-hidden="true">*</span></label>
      <input id="new-product-sku" name="sku" type="text"
        maxLength={64} value={sku} autoComplete="off" disabled={saving}
        placeholder="Ej. AGENDA-04"
        aria-invalid={Boolean(fieldError('sku'))}
        onChange={event => { setSku(event.target.value.toUpperCase()); setErrors({}); setMessage(null); }} />
      {fieldError('sku') && <small className="field-error">{fieldError('sku')}</small>}

      <label htmlFor="new-product-stock">Existencias iniciales <span aria-hidden="true">*</span></label>
      <input id="new-product-stock" name="disponible"
        type="number" min="0" max="1000000" step="1" inputMode="numeric"
        value={initialStock} disabled={saving} aria-invalid={Boolean(fieldError('disponible'))}
        onChange={event => { setInitialStock(event.target.value); setErrors({}); setMessage(null); }} />
      {fieldError('disponible') && <small className="field-error">{fieldError('disponible')}</small>}
      <p className="product-form-help">Puedes comenzar con cero y añadir unidades después. El SKU no podrá duplicarse.</p>

      <button className="primary-button" type="submit" disabled={saving}>
        {saving ? 'Registrando producto...' : 'Crear producto y conectar inventario'}
      </button>
      {message && (
        <FlashNotice tone="error" onClose={() => setMessage(null)}>{message}</FlashNotice>
      )}
    </form>
  );
}
