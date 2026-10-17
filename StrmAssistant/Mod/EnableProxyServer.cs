using Emby.Web.GenericEdit.Elements;
using HarmonyLib;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using static StrmAssistant.Common.CommonUtility;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    public class EnableProxyServer : PatchBase<EnableProxyServer>
    {
        private static MethodInfo _createHttpClientHandler;

        public EnableProxyServer()
        {
            Initialize();

            if (Plugin.Instance.NetworkProxyStore.GetOptions().Network.EnableProxyServer)
            {
                Patch();
            }
        }

        protected override void OnInitialize()
        {
            var embyServerImplementationsAssembly = Assembly.Load("Emby.Server.Implementations");
            var applicationHost =
                embyServerImplementationsAssembly.GetType("Emby.Server.Implementations.ApplicationHost");
            _createHttpClientHandler = applicationHost.GetMethod("CreateHttpClientHandler",
                BindingFlags.NonPublic | BindingFlags.Instance);
        }

        protected override void Prepare(bool apply)
        {
            PatchUnpatch(PatchTracker, apply, _createHttpClientHandler,
                postfix: nameof(CreateHttpClientHandlerPostfix));
        }

        [HarmonyPostfix]
        private static void CreateHttpClientHandlerPostfix(ref HttpMessageHandler __result)
        {
            var options = Plugin.Instance.NetworkProxyStore.GetOptions().Network;
            var proxyStatus = options.ProxyServerStatus.Status;
            var ignoreCertificateValidation = options.IgnoreCertificateValidation;

            if (Uri.TryCreate(options.ProxyServerUrl, UriKind.Absolute, out var proxyUri) &&
                proxyStatus == ItemStatus.Succeeded && TryParseProxyUrl(options.ProxyServerUrl, out var schema,
                    out var host, out var port, out var username, out var password))
            {
                var proxy = new SelectiveWebProxy(proxyUri, options.ProxyMode, options.ScrapeProxyWhitelist)
                {
                    Credentials = !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password)
                        ? new NetworkCredential(username, password)
                        : null
                };

                if (__result is HttpClientHandler httpClientHandler)
                {
                    httpClientHandler.Proxy = proxy;
                    httpClientHandler.UseProxy = true;
                    if (ignoreCertificateValidation)
                    {
                        httpClientHandler.ServerCertificateCustomValidationCallback =
                            (httpRequestMessage, cert, chain, sslErrors) => true;
                    }
                }
                else if (__result is SocketsHttpHandler socketsHttpHandler)
                {
                    socketsHttpHandler.Proxy = proxy;
                    socketsHttpHandler.UseProxy = true;
                    if (ignoreCertificateValidation)
                    {
                        socketsHttpHandler.SslOptions.RemoteCertificateValidationCallback =
                            (sender, cert, chain, sslErrors) => true;
                    }
                }
            }
        }

        /// <summary>
        /// 按路由模式决定是否走代理的 IWebProxy：
        /// Global 全部出站走代理（保留本地/私网直连）；ScrapeOnly 仅白名单后缀主机走代理。
        /// </summary>
        private sealed class SelectiveWebProxy : IWebProxy
        {
            private readonly Uri _proxyUri;
            private readonly NetworkOptions.ProxyRoutingMode _mode;
            private readonly string[] _whitelist;
            private static readonly string[] BypassHosts =
            {
                "localhost", "127.", "10.", "192.168.", "[::1]"
            };

            public SelectiveWebProxy(Uri proxyUri, NetworkOptions.ProxyRoutingMode mode, string whitelist)
            {
                _proxyUri = proxyUri;
                _mode = mode;
                _whitelist = (whitelist ?? string.Empty)
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(h => h.Trim().TrimStart('.').ToLowerInvariant())
                    .Where(h => h.Length > 0)
                    .ToArray();
            }

            public ICredentials Credentials { get; set; }

            public Uri GetProxy(Uri destination) => ShouldProxy(destination) ? _proxyUri : destination;

            public bool IsBypassed(Uri host) => !ShouldProxy(host);

            private bool ShouldProxy(Uri destination)
            {
                if (destination == null) return false;

                if (destination.IsLoopback) return false;

                var host = destination.Host.ToLowerInvariant();

                foreach (var bypass in BypassHosts)
                {
                    if (host.StartsWith(bypass)) return false;
                }

                // 私网 IPv4 文本段（10.x/172.16-31.x/192.168.x）
                if (System.Net.IPAddress.TryParse(host, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    var parts = host.Split('.');
                    if (parts[0] == "10" || parts[0] == "192" && parts[1] == "168" ||
                        parts[0] == "172" && int.TryParse(parts[1], out var second) &&
                        second >= 16 && second <= 31)
                    {
                        return false;
                    }
                }

                if (_mode == NetworkOptions.ProxyRoutingMode.Global) return true;

                // ScrapeOnly：白名单后缀匹配
                foreach (var suffix in _whitelist)
                {
                    if (host == suffix || host.EndsWith("." + suffix, StringComparison.Ordinal)) return true;
                }

                return false;
            }
        }
    }
}
