using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using ETS2LA.Logging;

namespace VisualizationSockets;

public class Websocket
{
    private readonly HttpListener _listener;
    private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new();
    private CancellationTokenSource? _cts;

    public Websocket(string prefix)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add(prefix);
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener.Start();
        Task.Run(() => ListenAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        
        foreach (var client in _clients.Values)
        {
            if (client.State == WebSocketState.Open)
            {
                client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server stopping", CancellationToken.None);
            }
            client.Dispose();
        }
        _clients.Clear();

        if (_listener.IsListening)
        {
            _listener.Stop();
        }
    }

    public async void Broadcast(string message)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(buffer);

        foreach (var (id, socket) in _clients)
        {
            try
            {
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    _clients.TryRemove(id, out var unused);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error sending data to client {id}: {ex.Message}");
            }
        }
    }

    private async Task ListenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    await ProcessWebSocketRequestAsync(context);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error accepting connection: {ex.Message}");
            }
        }
    }

    private async Task ProcessWebSocketRequestAsync(HttpListenerContext context)
    {
        WebSocketContext wsContext;
        try
        {
            wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
        }
        catch (Exception)
        {
            context.Response.StatusCode = 500;
            context.Response.Close();
            return;
        }

        var socket = wsContext.WebSocket;
        var connectionId = Guid.NewGuid();
        _clients.TryAdd(connectionId, socket);

        var buffer = new byte[1024 * 4];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }
            }
        }
        catch
        {
            Logger.Error("Error receiving data from client");
        }
        finally
        {
            _clients.TryRemove(connectionId, out var unused);
            socket.Dispose();
        }
    }
}