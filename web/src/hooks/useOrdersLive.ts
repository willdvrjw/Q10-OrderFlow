import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { useCallback, useEffect, useState } from 'react';
import { getOrders } from '../lib/api';
import type { Order } from '../types';

const POLLING_MS = 5000;

/**
 * Única fuente de estado de pedidos para el panel.
 * SignalR solo notifica que hubo cambios; GET /orders devuelve los datos reales.
 * Polling recupera coherencia cuando falla o se interrumpe SignalR.
 */
export function useOrdersLive() {
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [connected, setConnected] = useState(false);
  const dismissError = useCallback(() => setError(null), []);

  const refresh = useCallback(async (silent = false): Promise<void> => {
    try {
      const latest = await getOrders();
      setOrders(latest);
      setError(null);
    } catch {
      setError('No fue posible consultar los pedidos. Reintentaremos automáticamente.');
    } finally {
      if (!silent) setLoading(false);
    }
  }, []);

  useEffect(() => {
    let disposed = false;
    let connecting = false;
    void refresh();

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/orders')
      .withAutomaticReconnect([0, 1500, 4000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('ordersChanged', () => {
      if (!disposed) void refresh(true);
    });
    connection.onreconnecting(() => {
      if (!disposed) setConnected(false);
    });
    connection.onreconnected(() => {
      if (!disposed) {
        setConnected(true);
        void refresh(true);
      }
    });
    connection.onclose(() => {
      if (!disposed) setConnected(false);
    });

    // withAutomaticReconnect no reintenta fallos ocurridos durante el
    // PRIMER connection.start(). El arranque de Compose puede abrir el
    // frontend antes de que Orders API termine de inicializar PostgreSQL.
    const start = async () => {
      if (disposed || connecting ||
          connection.state !== HubConnectionState.Disconnected) return;
      connecting = true;
      try {
        await connection.start();
        if (!disposed) {
          setConnected(true);
          void refresh(true);
        }
      } catch {
        if (!disposed) setConnected(false);
      } finally {
        connecting = false;
      }
    };
    void start();

    // Polling independiente del socket, y reintento de la conexión inicial
    // en cada ciclo si el backend estuvo temporalmente no disponible.
    const interval = window.setInterval(() => {
      if (disposed) return;
      void refresh(true);
      void start();
    }, POLLING_MS);
    return () => {
      disposed = true;
      window.clearInterval(interval);
      void connection.stop();
    };
  }, [refresh]);

  return { orders, loading, error, connected, refresh, dismissError };
}
