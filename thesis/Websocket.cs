using System;
using WebSocketSharp;
using WebSocketSharp.Server;
public class Websocket
{
    private WebSocketServer Sever;
    private WebSocketSessionManager wsService;
    public Websocket(string port = "8080")
    {
        Sever = new WebSocketServer($"ws://0.0.0.0:{port}");
        Sever.AddWebSocketService<Service>("/webcam");
        Sever.Start();
        wsService = Sever.WebSocketServices["/webcam"].Sessions;
    }

    public void Send(byte[] data)
    {
        foreach (var session in wsService.Sessions)
        {
            session.Context.WebSocket.Send(data);
        }
    }
    public void Stop()
    {
        if (Sever.IsListening)
            Sever.Stop();

        if (Sever.IsListening)
            Sever.Stop();
    }
}

public class Service : WebSocketBehavior
{
    protected override void OnOpen()
    {
        base.OnOpen();
        Console.WriteLine("User connected!!");
    }

    protected override void OnClose(CloseEventArgs e)
    {
        base.OnClose(e);
        Console.WriteLine($"User disconnect!! {e}");
    }

    protected override void OnError(ErrorEventArgs e)
    {
        base.OnError(e);
        Console.WriteLine($"Eror: {e}");
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        base.OnMessage(e);
        Console.WriteLine($"FormUser: {e}");
    }
}



