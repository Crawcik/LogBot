using Newtonsoft.Json;
using System;
using System.Net;
using System.Threading.Tasks;

namespace LogBot
{
    class BotHandler
    {
        private string server_name;

        public Webhook webhook;
        private Channel channel;
        public BotHandler(string _server_name, string webhook_url = null)
        {
            if (webhook_url != null)
            {
                webhook = new Webhook(webhook_url);
                server_name = _server_name;
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

        public class Channel
        {
            private Uri _Uri;
            public Channel(string URL)
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
                    return wb.UploadString(_Uri, "PATCH", JsonConvert.SerializeObject(data));
                }
            }

            public struct Audit
            {
            
            }

            public struct Structure
            {
                public string name;
                public int posision;
                public string topic;
                public bool nsfw;
                public int rate_limit_per_user;
                public int bitrate;
                public int user_limit;
                public Permission[] permission_overwrites;
                public int parent_id;
            }

            public struct Permission
            {
                public int id;
                public string type;
                public int allow;
                public int deny;
            }
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
                public Image image;
                public Thumbnail thumbnail;
                public Video video;
                public Provider provider;
                public Author author;
                public Field[] fields;
            }

            public struct Field
            {
                public string name;
                public string value;
                public bool inline;
            }

            public struct Footer
            {
                public string text;
                public string icon_url;
                public string proxy_icon_url;
            }

            public struct Image
            {
                public string url;
                public string proxy_url;
                public int height;
                public int width;
            }

            public struct Thumbnail
            {
                public string url;
                public string proxy_url;
                public int height;
                public int width;
            }

            public struct Video
            {
                public string url;
                public int height;
                public int width;
            }

            public struct Provider
            {
                public string name;
                public string url;
            }

            public struct Author
            {
                public string name;
                public string url;
                public string icon_url;
                public string proxy_icon_url;
            }
        }
    }
}
