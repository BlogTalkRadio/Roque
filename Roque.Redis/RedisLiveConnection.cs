using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Cinchcast.Roque.Core;
using BookSleeve;

namespace Cinchcast.Roque.Redis
{
    public class RedisLiveConnection
    {
        private string _Host;
        private int _Port;
        private int _Timeout;

        private RedisConnection _Connection;

        private object syncConnection = new object();

        public RedisLiveConnection(IDictionary<string, string> settings)
        {
            if (!settings.TryGet("host", out _Host))
            {
                throw new Exception("Redis host is required");
            }
            _Port = settings.Get("port", 6379);
            _Timeout = settings.Get("timeout", 2000);
        }

        public RedisLiveConnection(string host, int port = 6379, int timeout = 2000)
        {
            _Host = host;
            _Port = port;
            _Timeout = timeout;
        }

        public RedisConnection GetOpen()
        {
            lock (syncConnection)
            {
                if (_Connection != null && !(_Connection.State == RedisConnectionBase.ConnectionState.Closed || _Connection.State == RedisConnectionBase.ConnectionState.Closing))
                {
                    return _Connection;
                }

                if (_Connection == null || (_Connection.State == RedisConnectionBase.ConnectionState.Closed || _Connection.State == RedisConnectionBase.ConnectionState.Closing))
                {
                    try
                    {
                        RoqueTrace.Source.Trace(TraceEventType.Information, "[REDIS] connecting to {0}:{1}", _Host, _Port);

                        _Connection = new RedisConnection(_Host, _Port, _Timeout);
                        var openAsync = _Connection.Open();
                        _Connection.Wait(openAsync);

                        RoqueTrace.Source.Trace(TraceEventType.Information, "[REDIS] connected");
                    }
                    catch (Exception ex)
                    {
                        RoqueTrace.Source.Trace(TraceEventType.Error, "[REDIS] error connecting to {0}:{1}, {2}", _Host, _Port, ex.Message, ex);
                        throw;
                    }
                }
                return _Connection;
            }
        }

        public RedisLiveConnection Clone()
        {
            return new RedisLiveConnection(_Host, _Port, _Timeout);
        }
    }
}
