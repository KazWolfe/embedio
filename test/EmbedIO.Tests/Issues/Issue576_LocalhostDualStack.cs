using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using NUnit.Framework;
using Swan;

namespace EmbedIO.Tests.Issues;

[TestFixture]
public class Issue576_LocalhostDualStack {
    [TestCase("127.0.0.1")]
    [TestCase("::1")]
    public async Task LocalhostAcceptsDualStack(string address) {
        if (SwanRuntime.OS != Swan.OperatingSystem.Windows)
            Assert.Ignore("Only Windows");

        using var instance = new WebServer(HttpListenerMode.EmbedIO, "http://localhost:8877");
        instance.OnAny(ctx => ctx.SendDataAsync(DateTime.Now));

        _ = instance.RunAsync();

        using var handler = BuildFakeResolver(IPAddress.Parse(address));
        using var client = new HttpClient(handler);

        var uri = new Uri("http://localhost:8877");
        Assert.IsNotEmpty(await client.GetStringAsync(uri).ConfigureAwait(false));
    }

    [TestCase("http://localhost:8877")] 
    [TestCase("http://127.0.0.1:8877")]
    public async Task LocalhostV4AcceptsIpAndHost(string uri) {
        if (SwanRuntime.OS != Swan.OperatingSystem.Windows)
            Assert.Ignore("Only Windows");

        using var instance = new WebServer(HttpListenerMode.EmbedIO, "http://localhost:8877", "http://127.0.0.1:8877");
        instance.OnAny(ctx => ctx.SendDataAsync(DateTime.Now));

        _ = instance.RunAsync();

        using var handler = BuildFakeResolver(IPAddress.Loopback); // force ipv4 for this test
        using var client = new HttpClient(handler);

        Assert.IsNotEmpty(await client.GetStringAsync(new Uri(uri)).ConfigureAwait(false));
    }
    
    [TestCase("http://localhost:8877")] 
    [TestCase("http://[::1]:8877")]
    public async Task LocalhostV6AcceptsIpAndHost(string uri) {
        if (SwanRuntime.OS != Swan.OperatingSystem.Windows)
            Assert.Ignore("Only Windows");

        using var instance = new WebServer(HttpListenerMode.EmbedIO, "http://localhost:8877", "http://[::1]:8877");
        instance.OnAny(ctx => ctx.SendDataAsync(DateTime.Now));

        _ = instance.RunAsync();

        using var handler = BuildFakeResolver(IPAddress.IPv6Loopback); // force ipv6 for this test
        using var client = new HttpClient(handler);

        Assert.IsNotEmpty(await client.GetStringAsync(new Uri(uri)).ConfigureAwait(false));
    }

    private static SocketsHttpHandler BuildFakeResolver(IPAddress target) {
        // Borrowed from https://www.meziantou.net/forcing-httpclient-to-use-ipv4-or-ipv6-addresses.htm and adapted
        return new SocketsHttpHandler {
            ConnectCallback = async (context, cancellationToken) => {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                socket.NoDelay = true;

                try {
                    await socket.ConnectAsync(target, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                } catch {
                    socket.Dispose();
                    throw;
                }
            },
        };
    }
}