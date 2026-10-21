# 07. Especificación UI/UX — OrderFlow Operations

## Objetivo

La interfaz representa una consola de operaciones de e-commerce, no una tienda para compradores. Debe ser clara para registrar pedidos, detectar errores y reconocer su estado en tiempo real. El diseño no compite con la solidez del backend.

## Identidad visual propia de la prueba

No copiar ni distribuir elementos de marca registrados de Q10 o Shopify. Referencia cromática general con acento naranja contenido, pero producto denominado OrderFlow y marca visual genérica.

| Token | Valor inicial propuesto | Uso |
|---|---|---|
| `--canvas` | `#F8FAFC` | Fondo general |
| `--surface` | `#FFFFFF` | Formulario, tabla y tarjetas |
| `--ink` | `#0F172A` | Texto principal |
| `--muted` | `#475569` | Texto secundario, con contraste legible |
| `--accent` | `#C2410C` | Acción principal, foco y navegación activa |
| `--accent-soft` | `#FFF7ED` | Matices de acento |
| `--pending` | `#92400E` | Estado pendiente con texto explicativo |
| `--confirmed` | `#166534` | Estado confirmado con texto explicativo |
| `--rejected` | `#B91C1C` | Estado rechazado con texto explicativo |
| `--border` | `#E2E8F0` | Separadores discretos |

Los colores se verificarán con contraste accesible y los estados siempre tendrán texto. Sombras suaves, bordes redondeados discretos, espaciado consistente y animaciones opcionales que respeten `prefers-reduced-motion`.

## Distribución desktop

Encabezado con nombre OrderFlow Operations y estado de conexión. Resumen de contadores Pending, Confirmed, Rejected. En el área principal: formulario a la izquierda y lista de pedidos a la derecha. Filtros por estado opcionales y compactos, sin esconder funcionalidad obligatoria. Registro de pedido presenta SKU, cantidad y nombre, errores cercanos al campo, estado de envío y confirmación de creación.

## Distribución responsive

En ancho reducido, una columna: encabezado > contadores > formulario > lista de pedidos. Evitar scroll horizontal y botones diminutos. Convertir tabla a tarjetas solo cuando sea necesario, preservando ID, cliente, SKU, cantidad, estado y fecha.

## Estados operativos

- Vacío: mensaje que explica cómo crear el primer pedido.
- Cargando: carga perceptible sin bloqueo innecesario.
- API inaccesible: banner legible y acción para reintentar; no inventar éxito.
- POST validación fallida: mensajes junto a campos y mensaje general con lenguaje claro.
- Pedido creado: se muestra `Pending` y se comunica que el inventario aún no se confirmó.
- Reservado: etiqueta `Confirmed`, cambio de estado con transición discreta.
- Sin stock: etiqueta `Rejected`, texto de motivo cuando el backend lo entregue.
- SignalR caído: mostrar «Actualizando periódicamente»; polling recupera el estado.

## Componentes propuestos

`AppShell`, `OrdersSummary`, `OrderForm`, `OrdersList`, `OrderStatusBadge`, `ConnectionStatus`, `FieldError`, `EmptyState`, `ToastRegion`. Hook `useOrdersLive` gestiona datos y conexión; los componentes de presentación no contienen llamadas directas a RabbitMQ ni reglas de inventario.

## Extensión de inventario y alta de productos

La consola incorpora un bloque de existencias bajo los pedidos: a la izquierda, tarjetas por SKU con desplazamiento vertical independiente y botón fijo **Agregar producto nuevo**; a la derecha, el formulario de ajuste de unidades. Un `ResizeObserver` permite que la columna izquierda nunca exceda la altura calculada del editor, incluso si se crean muchos productos. En pantallas estrechas, ambas columnas se apilan conservando el área desplazable.

Al pulsar el botón de alta, se abre `NewProductForm` dentro del área desplazable, sin ocultar permanentemente las tarjetas. Exige un nombre, un SKU único y existencias iniciales (pueden ser cero). La API debe confirmar ambos registros antes de mostrar el resultado como disponible; luego se refrescan `GET /products` y `GET /inventory` y se selecciona la nueva tarjeta.

El texto introductorio del editor mantiene alineación izquierda, diferenciándolo de las notificaciones flash centradas que sí pueden cerrarse manualmente. Inputs y selects poseen `id` y `name` compatibles con DevTools/autocompletado. Los errores de una extensión de Chrome se revisan fuera del código de la aplicación, sin esconder advertencias ni errores de consola.

## Accesibilidad y pruebas

Labels asociadas a inputs, tabulación coherente, contraste, estados de foco, `aria-live` para resultados importantes y errores de formulario, no usar exclusivamente verde/rojo para comunicar estado. Verificar escritorio y móvil, zoom de navegador, envío por teclado y simulación de conexión interrumpida.
