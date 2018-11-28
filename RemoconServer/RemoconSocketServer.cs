using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RemoconServer
{
    public enum ClientType
    {
        Sender, Receiver
    }

    public class WebSocketConnectionCloseException: Exception
    {
        public bool Premature;

        public WebSocketConnectionCloseException(bool premature)
        {
            this.Premature = premature;
        }
    }

    public static class RemoconSocketServer
    {
        private static readonly List<WebSocket> Senders = new List<WebSocket>();
        private static readonly List<WebSocket> Receivers = new List<WebSocket>();

        public static async Task AddClient(HttpContext context, Func<Task> next)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                await next();
                return;
            }

            int? port = context.Request.Host.Port;
            string path = context.Request.Path;

            WebSocket client = await context.WebSockets.AcceptWebSocketAsync();
            ClientType type;
            if (port == Program.SenderPort && path == "/sender")
            {
                Senders.Add(client);
                type = ClientType.Sender;
            }
            else if (port == Program.ReceiverPort && path == "/receiver")
            {
                Receivers.Add(client);
                type = ClientType.Receiver;
            }
            else
            {
                await client.CloseAsync(WebSocketCloseStatus.EndpointUnavailable,
                    "Unknown port or path", CancellationToken.None);
                await next();
                return;
            }

            await BroadcastReceiversAmount();
            try
            {
                await ClientTask(context, client, type);
            }
            catch (WebSocketConnectionCloseException ex)
            {
                if (type == ClientType.Sender)
                {
                    Senders.Remove(client);
                }
                else if (type == ClientType.Receiver)
                {
                    Receivers.Remove(client);
                    await BroadcastReceiversAmount();
                }
            }
        }

        private static async Task SendMessage(byte[] message, WebSocket client)
        {
            byte[] buffer;
            for (int i = 0; i < message.Length; i += Program.BufferSize)
            {
                int bufferLength = Math.Min(Program.BufferSize, message.Length - i);
                buffer = new byte[bufferLength];
                Array.Copy(message, i, buffer, 0, bufferLength);

                bool endOfMessage = i + Program.BufferSize >= message.Length;

                await client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text,
                    endOfMessage, CancellationToken.None);
            }
        }

        private static async Task Broadcast(string message, List<WebSocket> clients)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            IEnumerable<Task> tasks = clients.Select(client => SendMessage(messageBytes, client));
            await Task.WhenAll(tasks);
        }

        private static async Task BroadcastToSenders(string message)
        {
            await Broadcast(message, Senders);
        }

        private static async Task BroadcastToReceivers(string message)
        {
            await Broadcast(message, Receivers);
        }

        private static async Task BroadcastReceiversAmount()
        {
            await Broadcast($"RECEIVERS={Receivers.Count}", Senders);
        }

        private static byte[] TrimEnd(byte[] array)
        {
            int lastIndex = Array.FindLastIndex(array, b => b != 0);
            if (lastIndex >= 0)
            {
                Array.Resize(ref array, lastIndex + 1);
            }
            return array;
        }

        private static async Task ClientTask(HttpContext context, WebSocket client, ClientType type)
        {
            try
            {
                string message;
                byte[] buffer;
                WebSocketReceiveResult result;
                do
                {
                    buffer = new byte[Program.BufferSize];
                    result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    message = Encoding.UTF8.GetString(TrimEnd(buffer));
                    Console.WriteLine($"Message: {message}");
                    if (type == ClientType.Sender)
                    {
                        if (message == "RECEIVERS_COUNT")
                        {
                            await SendMessage(Encoding.UTF8.GetBytes(Receivers.Count.ToString()), client);
                        }
                        else
                        {
                            await BroadcastToReceivers(message);
                        }
                    }
                    else if (type == ClientType.Receiver)
                    {
                        if (message == "SENDERS_COUNT")
                        {
                            await SendMessage(Encoding.UTF8.GetBytes(Senders.Count.ToString()), client);
                        }
                        else
                        {
                            await BroadcastToSenders(message);
                        }
                    }
                }
                while (result.MessageType != WebSocketMessageType.Close);
                await client.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                throw new WebSocketConnectionCloseException(false);
            }
            catch (WebSocketException ex)
            {
                if (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    throw new WebSocketConnectionCloseException(true);
                }
                else
                {
                    throw ex;
                }
            }
        }
    }
}
