﻿using Newtonsoft.Json.Linq;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TonSdk
{
    public partial class TonClient : ITonClient
    {
        private uint _context;
        private volatile bool _initialized;
        private readonly TonSerializer _serializer;
        internal readonly object Config;

        internal ILogger Logger { get; }

        public static ITonClient Create(ILogger logger = null)
        {
            return Create(null, logger);
        }

        public static ITonClient Create(object config, ILogger logger = null)
        {
            var client = new TonClient(config ?? new { }, logger);
            client.Init();
            return client;
        }

        private TonClient(object config, ILogger logger)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Logger = logger ?? DummyLogger.Instance;
            _serializer = new TonSerializer(Logger);
        }

        private void Init()
        {
            _context = CreateContext();
            _initialized = true;
        }

        public void Dispose()
        {
            if (_initialized)
            {
                Interop.tc_destroy_context(_context);
            }
        }

        public async Task<T> CallFunctionAsync<T>(string functionName, object @params = null)
        {
            var result = await GetJsonResponse<T>(functionName, @params);
            return _serializer.Deserialize<T>(result);
        }

        public async Task CallFunctionAsync(string functionName, object @params = null)
        {
            await GetJsonResponse<string>(functionName, @params);
        }

        public async Task<T> CallFunctionAsync<T, TC>(string functionName, object @params, Action<TC, int> callback)
        {
            var result = await GetJsonResponse(functionName, @params, callback);
            return _serializer.Deserialize<T>(result);
        }

        public async Task CallFunctionAsync<TC>(string functionName, object @params, Action<TC, int> callback)
        {
            await GetJsonResponse(functionName, @params, callback);
        }

        private async Task<string> GetJsonResponse<TC>(string functionName, object @params, Action<TC, int> callback = null)
        {
            var functionParamsJson = @params != null
                ? _serializer.Serialize(@params)
                : "";

            Logger.Debug($"Calling function {functionName} with parameters {functionParamsJson}");

            // GCHandle to store reference to the native callback handler.
            // This is to avoid native handler being garbage collected while waiting
            // for result from the native lib.

            var callbackHandle = default(GCHandle);

            var tcs = new TaskCompletionSource<string>();

            var handler = new Interop.tc_response_handler_t((requestId, json_str, type, finished) =>
            {
                try
                {
                    var json = json_str.ToString();
                    Logger.Debug($"{functionName} status update: {type} ({json})");
                    if (type == (int)Interop.tc_response_types_t.tc_response_success)
                    {
                        tcs.SetResult(json);
                    }
                    else if (type == (int)Interop.tc_response_types_t.tc_response_error)
                    {
                        tcs.SetException(TonClientException.FromJson(json));
                    }
                    else if (type == (int)Interop.tc_response_types_t.tc_response_nop)
                    {
                        // TODO: ???
                    }
                    else
                    {
                        if (callback != null)
                        {
                            var value = _serializer.Deserialize<TC>(json);
                            callback.Invoke(value, (int)type);
                        }
                    }
                }
                finally
                {
                    if (finished && callbackHandle.IsAllocated)
                    {
                        callbackHandle.Free();
                    }
                }
            });

            callbackHandle = GCHandle.Alloc(handler);

            using (var fNameStr = new TonString(functionName))
            {
                using (var fParamsStr = new TonString(functionParamsJson))
                {
                    Interop.tc_request(_context, fNameStr.ToStruct(), fParamsStr.ToStruct(), 1, handler);
                }
            }

            return await tcs.Task;
        }

        private uint CreateContext()
        {
            Logger.Debug("Init context");

            uint context = 0;

            using (var configStr = new TonString(_serializer.Serialize(Config)))
            {
                var result = Interop.tc_create_context(configStr.ToStruct());
                if (result == IntPtr.Zero)
                {
                    Logger.Error("Init context returned null");
                    throw new TonClientException($"{nameof(Interop.tc_create_context)} returned null");
                }

                var json = Interop.tc_read_string(result).ToString();
                Logger.Debug($"Init context returned JSON: {json}");
                Interop.tc_destroy_string(result);

                var token = JObject.Parse(json);
                if (token.TryGetValue("result", out var contextToken))
                {
                    Logger.Debug($"Init context succeeded: {contextToken}");
                    context = contextToken.Value<uint>();
                }
                else
                {
                    if (token.TryGetValue("error", out var errorToken))
                    {
                        Logger.Debug($"throwing exception with error {errorToken}");
                        throw TonClientException.FromJson(errorToken.Value<JToken>());
                    }
                    else
                    {
                        Logger.Debug($"throwing exception with the returned JSON: {json}");
                        throw TonClientException.FromJson(json);
                    }
                }
            }

            return context;
        }
    }

    internal class TonString : IDisposable
    {
        private readonly string _str;
        private readonly IntPtr _content;
        private readonly uint _len;

        public TonString(string str)
        {
            _str = str;
            var bytes = Encoding.UTF8.GetBytes(str);
            var length = bytes.Length;
            _content = Marshal.AllocHGlobal(length);
            Marshal.Copy(bytes, 0, _content, length);
            _len = (uint)length;
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(_content);
        }

        public Interop.tc_string_data_t ToStruct()
        {
            return new Interop.tc_string_data_t
            {
                content = _content,
                len = _len
            };
        }

        public override string ToString()
        {
            return _str;
        }
    }
}
