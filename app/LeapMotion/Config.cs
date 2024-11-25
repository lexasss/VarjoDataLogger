/******************************************************************************
 * Copyright (C) Ultraleap, Inc. 2011-2020.                                   *
 *                                                                            *
 * Use subject to the terms of the Apache License 2.0 available at            *
 * http://www.apache.org/licenses/LICENSE-2.0, or another agreement           *
 * between Ultraleap and you, your company or other organization.             *
 ******************************************************************************/

namespace Leap
{

    using System;
    using System.Collections.Generic;
    using LeapInternal;

    /// <summary>
    /// The Config class provides access to Leap Motion system configuration information.
    /// 
    /// @since 1.0
    /// </summary>
    public class Config
    {
        private Connection _connection;
        private Dictionary<UInt32, object> _transactions = new Dictionary<UInt32, object>();

        /// <summary>
        /// Creates a new Config object for setting runtime configuration settings.
        /// 
        /// Note that the LeapMotion.Config provides a properly initialized Config object already.
        /// @since 3.0
        /// </summary>
        public Config(int connectionKey)
        {
            _connection = Connection.GetConnection(connectionKey);
            _connection.LeapConfigChange += handleConfigChange;
            _connection.LeapConfigResponse += handleConfigResponse;
        }

        private void handleConfigChange(object? sender, ConfigChangeEventArgs? eventArgs)
        {
            if (eventArgs == null)
                return;

            if (_transactions.TryGetValue(eventArgs.RequestId, out object? actionDelegate))
            {
                Action<bool>? changeAction = actionDelegate as Action<bool>;
                changeAction?.Invoke(eventArgs.Succeeded);
                _transactions.Remove(eventArgs.RequestId);
            }
        }

        private void handleConfigResponse(object? sender, SetConfigResponseEventArgs? eventArgs)
        {
            if (eventArgs == null)
                return;

            if (_transactions.TryGetValue(eventArgs.RequestId, out object? actionDelegate))
            {
                switch (eventArgs.DataType)
                {
                    case ValueType.TYPE_BOOLEAN:
                        Action<bool>? boolAction = actionDelegate as Action<bool>;
                        boolAction?.Invoke((int)eventArgs.Value != 0);
                        break;
                    case ValueType.TYPE_FLOAT:
                        Action<float>? floatAction = actionDelegate as Action<float>;
                        floatAction?.Invoke((float)eventArgs.Value);
                        break;
                    case ValueType.TYPE_INT32:
                        Action<Int32>? intAction = actionDelegate as Action<Int32>;
                        intAction?.Invoke((Int32)eventArgs.Value);
                        break;
                    case ValueType.TYPE_STRING:
                        Action<string>? stringAction = actionDelegate as Action<string>;
                        stringAction?.Invoke((string)eventArgs.Value);
                        break;
                    default:
                        break;
                }
                _transactions.Remove(eventArgs.RequestId);
            }
        }

        /// <summary>
        /// Requests a configuration value.
        /// 
        /// You must provide an action to take when the Leap service returns the config value.
        /// The Action delegate must take a parameter matching the config value type. The current
        /// value of the setting is passed to this delegate.
        /// 
        /// @since 3.0
        /// </summary>
        public bool Get<T>(string key, Action<T> onResult)
        {
            uint requestId = _connection.GetConfigValue(key);
            if (requestId > 0)
            {
                _transactions.Add(requestId, onResult);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sets a configuration value.
        /// 
        /// You must provide an action to take when the Leap service sets the config value.
        /// The Action delegate must take a boolean parameter. The service calls this delegate
        /// with the value true if the setting was changed successfully and false, otherwise.
        /// 
        /// @since 3.0
        /// </summary>
        public bool Set<T>(string key, T value, Action<bool> onResult) where T : IConvertible
        {
            uint requestId = _connection.SetConfigValue<T>(key, value);

            if (requestId > 0)
            {
                _transactions.Add(requestId, onResult);
                return true;
            }
            return false;
        }

        [Obsolete("Use the generic Set<T> method instead.")]
        public ValueType Type(string key)
        {
            return ValueType.TYPE_UNKNOWN;
        }

        [Obsolete("Use the generic Get<T> method instead.")]
        public bool GetBool(string key)
        {
            return false;
        }

        [Obsolete("Use the generic Set<T> method instead.")]
        public bool SetBool(string key, bool value)
        {
            return false;
        }

        [Obsolete("Use the generic Get<T> method instead.")]
        public bool GetInt32(string key)
        {
            return false;
        }

        [Obsolete("Use the generic Set<T> method instead.")]
        public bool SetInt32(string key, int value)
        {
            return false;
        }

        [Obsolete("Use the generic Get<T> method instead.")]
        public bool GetFloat(string key)
        {
            return false;
        }

        [Obsolete("Use the generic Set<T> method instead.")]
        public bool SetFloat(string key, float value)
        {
            return false;
        }

        [Obsolete("Use the generic Get<T> method instead.")]
        public bool GetString(string key)
        {
            return false;
        }

        [Obsolete("Use the generic Set<T> method instead.")]
        public bool SetString(string key, string value)
        {
            return false;
        }

        [Obsolete]
        public bool Save()
        {
            return false;
        }

        /// <summary>
        /// Enumerates the possible data types for configuration values.
        /// @since 1.0
        /// </summary>
        public enum ValueType
        {
            TYPE_UNKNOWN = 0,
            TYPE_BOOLEAN = 1,
            TYPE_INT32 = 2,
            TYPE_FLOAT = 6,
            TYPE_STRING = 8,
        }
    }
}
