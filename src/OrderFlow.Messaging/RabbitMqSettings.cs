using RabbitMQ.Client;

namespace OrderFlow.Messaging;

/// <summary>
/// Opciones compartidas de RabbitMQ; los valores provienen de variables de entorno.
/// No se incluyen contraseñas ni direcciones de producción en el código.
/// </summary>
public sealed record RabbitMqSettings(string HostName, string UserName, string Password, int Port = 5672)
{
    public ConnectionFactory CreateFactory(string connectionName)
    {
        if (string.IsNullOrWhiteSpace(HostName) ||
            string.IsNullOrWhiteSpace(UserName) ||
            string.IsNullOrWhiteSpace(Password) ||
            Port is < 1 or > 65535)
            throw new InvalidOperationException("Configuración de RabbitMQ inválida.");

        return new ConnectionFactory
        {
            HostName = HostName,
            Port = Port,
            UserName = UserName,
            Password = Password,
            ClientProvidedName = connectionName,
            // El ciclo de reintentos de los servicios controla conexiones perdidas
            // y reconecta tanto consumidores como publicadores desde cero.
            AutomaticRecoveryEnabled = false,
            RequestedHeartbeat = TimeSpan.FromSeconds(15)
        };
    }
}
