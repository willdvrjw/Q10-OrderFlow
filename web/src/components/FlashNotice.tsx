import type { ReactNode } from 'react';

type NoticeTone = 'success' | 'error' | 'info';

interface Props {
  tone: NoticeTone;
  children: ReactNode;
  onClose: () => void;
  className?: string;
}

/**
 * Mensaje de estado reutilizable: texto centrado y cierre manual accesible.
 * Ocultar la notificación no altera validaciones ni operaciones registradas.
 */
export function FlashNotice({ tone, children, onClose, className }: Props) {
  return (
    <div
      className={'notice ' + tone + (className ? ' ' + className : '')}
      role={tone === 'error' ? 'alert' : 'status'}
    >
      <div className="notice-body">{children}</div>
      <button
        type="button"
        className="notice-close"
        aria-label="Cerrar mensaje"
        title="Cerrar mensaje"
        onClick={onClose}
      >
        <span aria-hidden="true">×</span>
      </button>
    </div>
  );
}
