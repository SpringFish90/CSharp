using System.Net;
using System.Net.WebSockets;
using System.Text;

public class WebSocketHandler
{
    public static async Task HandleWebSocketRequestConsole()
    {
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:7143/ws/");
        listener.Start();
        Console.WriteLine("웹소켓 서버가 시작되었습니다...");

        while (true)
        {
            var context = await listener.GetContextAsync();
            if (context.Request.IsWebSocketRequest)
            {
                var wsContext = await context.AcceptWebSocketAsync(null);
                using var webSocket = wsContext.WebSocket;

                await HandleClient(webSocket);
            }
            else
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
            }
        }
    }

    public async Task HandleWebSocketRequest(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        using WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await HandleClient(webSocket);
    }

    private static async Task HandleClient(WebSocket webSocket)
    {
        byte[] buffer = new byte[1024 * 4];

        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine($"받은 메시지: {message}");

            // 클라이언트에게 받은 메시지를 그대로 전송 (에코)
            byte[] sendBuffer = Encoding.UTF8.GetBytes($"{message}");
            await webSocket.SendAsync(new ArraySegment<byte>(sendBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        Console.WriteLine("클라이언트 연결 종료");
    }
}
