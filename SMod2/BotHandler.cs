using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

namespace LogBot
{
    class BotHandler
    {
        public Webhook webhook;
        public PipeStream stream;
        public BotHandler(string webhook_url, string bot_indetificator = null)
        {
            if (webhook_url != null)
            {
                webhook = new Webhook(webhook_url);
            }
            if (bot_indetificator != null)
            {
                PipingToBot(bot_indetificator).GetAwaiter();
            }
        }

        public async Task SendToBot(MessageType cel, object data)
        {
            if (stream == null)
                return;
            if (!stream.IsConnected)
                return;
            byte[] buf = new Message((byte)cel, data).Serialize();
            stream.Write(buf, 0, buf.Length);
            await stream.FlushAsync();
        }

        public async Task<Message> WaitForMessage()
        {
            byte[] buf = new byte[1024];
            await stream.ReadAsync(buf, 0, buf.Length);
            return Message.Deserialize(buf);
        }

        private async Task PipingToBot(string name)
        {
            var stream = new NamedPipeServerStream(name, PipeDirection.InOut);
            await stream.WaitForConnectionAsync();
            this.stream = stream;
        }

        public void Post(string text, string description, string killer_id, int color)
        {
            Webhook.Structure obj = new Webhook.Structure()
            {
                username = "Log Bot",
                embeds = new Webhook.Embed[]
                {
                    new Webhook.Embed()
                    { 
                        title = text,
                        description = description,
                        color = color,
                        footer = new Webhook.Footer()
                        {
                            text = killer_id
                        }
                    }
                }
                
            };
            webhook.PostData(obj);
        }

        public class Webhook
        {

            private Uri _Uri;
            public Webhook(string URL)
            {
                if (!Uri.TryCreate(URL, UriKind.Absolute, out _Uri))
                {
                    throw new UriFormatException();
                }
            }

            public string PostData(Structure data)
            {
                using (WebClient wb = new WebClient())
                {
                    wb.Headers.Add(HttpRequestHeader.ContentType, "application/json");
                    return wb.UploadString(_Uri, "POST", JsonConvert.SerializeObject(data));
                }
            }
            public struct Structure
            {
                public string content;
                public string username;
                public string avatar_url;
                public Embed[] embeds;
            }

            public struct Embed
            {
                public string title;
                public string type;
                public string description;
                public string url;
                public int color;
                public Footer footer;
            }

            public struct Footer
            {
                public string text;
                public string icon_url;
                public string proxy_icon_url;
            }
        }
        [Serializable]
        public struct Message
        {
            public byte destiny { private set; get; }
            public object data { private set; get; }

            public Message(byte destiny, object data)
            {
                this.destiny = destiny;
                this.data = data;
            }

            public static Message Deserialize(byte[] raw_data)
            {
                var stream = new MemoryStream(raw_data);
                var formatter = new BinaryFormatter();
                return (Message)formatter.Deserialize(stream);
            }

            public byte[] Serialize()
            {
                var formatter = new BinaryFormatter();
                var stream = new MemoryStream();
                formatter.Serialize(stream, this);
                return stream.ToArray();
            }
        }
    }
    public enum MessageType
    {
        SWITCH_LOGBOT,
        SWITCH_AUTOBANS,
        SERVER_COUNT,
        BAN,
        UNBAN,
        ERROR
    }
}
