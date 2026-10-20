-- Se ejecuta automáticamente por la imagen oficial de PostgreSQL al crear
-- el volumen por primera vez. Separación lógica de datos por servicio:
-- ninguna aplicación consulta la base propiedad de otro servicio.
CREATE DATABASE orderflow_orders;
CREATE DATABASE orderflow_inventory;
