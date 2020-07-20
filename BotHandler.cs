using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;

namespace LogBot
{
    class BotHandler
    {
        public Webhook webhook;
        public BotHandler(string webhook_url)
        {
            if (webhook_url != null)
            {
                webhook = new Webhook(webhook_url);
            }
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
    }
}
