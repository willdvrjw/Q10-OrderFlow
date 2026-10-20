import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
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
    void refresh();

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/orders')
      .withAutomaticReconnect([0, 1500, 4000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('ordersChanged', () => void refresh(true));
    connection.onreconnecting(() => setConnected(false));
    connection.onreconnected(() => {
      setConnected(true);
      void refresh(true);
    });
    connection.onclose(() => setConnected(false));

    void connection.start()
      .then(() => {
        if (!disposed) setConnected(true);
      })
      .catch(() => {
        if (!disposed) setConnected(false);
      });

    const interval = window.setInterval(() => void refresh(true), POLLING_MS);
    return () => {
      disposed = true;
      window.clearInterval(interval);
      void connection.stop();
    };
  }, [refresh]);

  return { orders, loading, error, connected, refresh };
}
