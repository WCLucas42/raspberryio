﻿namespace Unosquare.RaspberryIO
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Represents a low-level exception, typically thrown when return codes from a
    /// low-level operation is non-zero or in some cases when it is less than zero.
    /// </summary>
    /// <seealso cref="System.Exception" />
    public class HardwareException : Exception
    {
        /// <summary>
        /// Throws a new instance of a hardware error by retrieving the last error number (errno).
        /// </summary>
        /// <param name="className">Name of the class.</param>
        /// <param name="methodName">Name of the method.</param>
        /// <exception cref="Unosquare.RaspberryIO.HardwareException"></exception>
        static public void Throw(string className, string methodName)
        {
            var errno = Marshal.GetLastWin32Error();
            throw new HardwareException(errno, $"{className}.{methodName}");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HardwareException" /> class.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        /// <param name="component">The component.</param>
        public HardwareException(int errorCode, string component)
            : base($"A hardware exception occurred. Error Code: {errorCode}")
        {
            ExtendedMessage = null;

            try
            {
                ExtendedMessage = Interop.strerror(errorCode);
            }
            catch
            {
                // TODO: strerror not working great...
                Console.WriteLine("BAD STUFF OCCURRED");
            }

            ErrorCode = errorCode;
            Component = component;
        }

        /// <summary>
        /// Gets the error code.
        /// </summary>
        /// <value>
        /// The error code.
        /// </value>
        public int ErrorCode { get; }

        /// <summary>
        /// Gets the component.
        /// </summary>
        /// <value>
        /// The component.
        /// </value>
        public string Component { get; }

        /// <summary>
        /// Gets the extended message (could be null).
        /// </summary>
        /// <value>
        /// The extended message.
        /// </value>
        public string ExtendedMessage { get; private set; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        /// <PermissionSet>
        ///   <IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" PathDiscovery="*AllFiles*" />
        /// </PermissionSet>
        public override string ToString()
        {
            return $"{GetType()}{(string.IsNullOrWhiteSpace(Component) ? "" : $" on {Component}")}: ({ErrorCode}) - {Message}";
        }
    }
}
