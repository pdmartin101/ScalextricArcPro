using System;

namespace ScalextricBle;

/// <summary>
/// Base exception class for all BLE-related errors.
/// </summary>
public class BleException : Exception
{
    /// <summary>
    /// Initializes a new instance of the BleException class.
    /// </summary>
    public BleException() : base() { }

    /// <summary>
    /// Initializes a new instance of the BleException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public BleException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the BleException class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public BleException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when a BLE scanning operation fails.
/// </summary>
public class BleScanException : BleException
{
    /// <summary>
    /// Initializes a new instance of the BleScanException class.
    /// </summary>
    public BleScanException() : base("BLE scanning operation failed") { }

    /// <summary>
    /// Initializes a new instance of the BleScanException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public BleScanException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the BleScanException class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public BleScanException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when a BLE connection operation fails.
/// </summary>
public class BleConnectionException : BleException
{
    /// <summary>
    /// Initializes a new instance of the BleConnectionException class.
    /// </summary>
    public BleConnectionException() : base("BLE connection operation failed") { }

    /// <summary>
    /// Initializes a new instance of the BleConnectionException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public BleConnectionException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the BleConnectionException class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public BleConnectionException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when a BLE GATT service discovery operation fails.
/// </summary>
public class BleServiceDiscoveryException : BleException
{
    /// <summary>
    /// Gets the UUID of the service that failed to be discovered.
    /// </summary>
    public Guid? ServiceUuid { get; }

    /// <summary>
    /// Initializes a new instance of the BleServiceDiscoveryException class.
    /// </summary>
    public BleServiceDiscoveryException() : base("BLE service discovery failed") { }

    /// <summary>
    /// Initializes a new instance of the BleServiceDiscoveryException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public BleServiceDiscoveryException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the BleServiceDiscoveryException class with a service UUID and message.
    /// </summary>
    /// <param name="serviceUuid">The UUID of the service that failed to be discovered.</param>
    /// <param name="message">The message that describes the error.</param>
    public BleServiceDiscoveryException(Guid serviceUuid, string message) : base(message)
    {
        ServiceUuid = serviceUuid;
    }

    /// <summary>
    /// Initializes a new instance of the BleServiceDiscoveryException class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public BleServiceDiscoveryException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when a BLE characteristic read operation fails.
/// </summary>
public class BleCharacteristicReadException : BleException
{
    /// <summary>
    /// Gets the UUID of the characteristic that failed to be read.
    /// </summary>
    public Guid? CharacteristicUuid { get; }

    /// <summary>
    /// Initializes a new instance of the BleCharacteristicReadException class.
    /// </summary>
    public BleCharacteristicReadException() : base("BLE characteristic read failed") { }

    /// <summary>
    /// Initializes a new instance of the BleCharacteristicReadException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public BleCharacteristicReadException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the BleCharacteristicReadException class with a characteristic UUID and message.
    /// </summary>
    /// <param name="characteristicUuid">The UUID of the characteristic that failed to be read.</param>
    /// <param name="message">The message that describes the error.</param>
    public BleCharacteristicReadException(Guid characteristicUuid, string message) : base(message)
    {
        CharacteristicUuid = characteristicUuid;
    }

    /// <summary>
    /// Initializes a new instance of the BleCharacteristicReadException class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public BleCharacteristicReadException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when a BLE characteristic write operation fails.
/// </summary>
public class BleCharacteristicWriteException : BleException
{
    /// <summary>
    /// Gets the UUID of the characteristic that failed to be written.
    /// </summary>
    public Guid? CharacteristicUuid { get; }

    /// <summary>
    /// Initializes a new instance of the BleCharacteristicWriteException class.
    /// </summary>
    public BleCharacteristicWriteException() : base("BLE characteristic write failed") { }

    /// <summary>
    /// Initializes a new instance of the BleCharacteristicWriteException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public BleCharacteristicWriteException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the BleCharacteristicWriteException class with a characteristic UUID and message.
    /// </summary>
    /// <param name="characteristicUuid">The UUID of the characteristic that failed to be written.</param>
    /// <param name="message">The message that describes the error.</param>
    public BleCharacteristicWriteException(Guid characteristicUuid, string message) : base(message)
    {
        CharacteristicUuid = characteristicUuid;
    }

    /// <summary>
    /// Initializes a new instance of the BleCharacteristicWriteException class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public BleCharacteristicWriteException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when a BLE operation times out.
/// </summary>
public class BleTimeoutException : BleException
{
    /// <summary>
    /// Gets the name of the operation that timed out.
    /// </summary>
    public string? OperationName { get; }

    /// <summary>
    /// Gets the timeout duration.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Initializes a new instance of the BleTimeoutException class.
    /// </summary>
    public BleTimeoutException() : base("BLE operation timed out")
    {
        Timeout = TimeSpan.Zero;
    }

    /// <summary>
    /// Initializes a new instance of the BleTimeoutException class with an operation name and timeout.
    /// </summary>
    /// <param name="operationName">The name of the operation that timed out.</param>
    /// <param name="timeout">The timeout duration.</param>
    public BleTimeoutException(string operationName, TimeSpan timeout)
        : base($"BLE operation '{operationName}' timed out after {timeout.TotalSeconds} seconds")
    {
        OperationName = operationName;
        Timeout = timeout;
    }

    /// <summary>
    /// Initializes a new instance of the BleTimeoutException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public BleTimeoutException(string message) : base(message)
    {
        Timeout = TimeSpan.Zero;
    }

    /// <summary>
    /// Initializes a new instance of the BleTimeoutException class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public BleTimeoutException(string message, Exception innerException) : base(message, innerException)
    {
        Timeout = TimeSpan.Zero;
    }
}
