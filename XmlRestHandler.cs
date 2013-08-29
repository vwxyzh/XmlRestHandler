using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace MvcApplication1
{
    public class XmlRestHandler
        : HttpMessageHandler
    {
        private readonly string m_path;

        public XmlRestHandler(string path)
        {
            m_path = HttpContext.Current.Server.MapPath(path);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                var rd = request.GetRouteData();
                var path = rd.Values["path"] as string;
                var ns = new XmlNamespaceManager(new NameTable());
                foreach (var p in request.GetQueryNameValuePairs())
                    ns.AddNamespace(p.Key, p.Value);
                if (request.Method == HttpMethod.Get)
                {
                    return HandleGet(path, ns);
                }
                else if (request.Method == HttpMethod.Post)
                {
                    var value = await request.Content.ReadAsStringAsync();
                    return HandlePost(path, ns, value);
                }
                else if (request.Method == HttpMethod.Put)
                {
                    var value = await request.Content.ReadAsStringAsync();
                    return HandlePut(path, ns, value);
                }
                else if (request.Method == HttpMethod.Delete)
                {
                    return HandleDelete(path, ns);
                }
                else
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.MethodNotAllowed);
                }
            }
            catch (Exception ex)
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(ex.ToString())
                };
            }
        }

        private HttpResponseMessage HandleGet(string path, IXmlNamespaceResolver ns)
        {
            var doc = XDocument.Load(m_path);
            var result = doc.XPathEvaluate(path, ns);
            //a bool, a double, a string, or an System.Collections.Generic.IEnumerable
            if (result is IEnumerable<object>)
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Join("\n",
                        (from x in ((IEnumerable<object>)result).OfType<XObject>()
                         select x.ToString())))
                };
            }
            else if (result is string || result is bool || result is double)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(result.ToString())
                };
            }
            else
                return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private HttpResponseMessage HandlePost(string path, IXmlNamespaceResolver ns, string value)
        {
            var doc = XDocument.Load(m_path);
            var nodes = doc.XPathSelectElements(path, ns);
            XElement element;
            try
            {
                element = XElement.Parse(value);
            }
            catch (Exception)
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("post element please.")
                };
            }
            bool found = false;
            foreach (var item in nodes)
            {
                found = true;
                item.Add(element);
            }
            if (found)
            {
                doc.Save(m_path);
                return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
            }
            else
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            }
        }

        private HttpResponseMessage HandlePut(string path, IXmlNamespaceResolver ns, string value)
        {
            var doc = XDocument.Load(m_path);
            var result = doc.XPathEvaluate(path, ns);
            if (result is IEnumerable<object>)
            {
                bool found = false;
                foreach (var item in (IEnumerable<object>)result)
                {
                    var xobj = item as XObject;
                    if (xobj != null)
                    {
                        switch (xobj.NodeType)
                        {
                            case System.Xml.XmlNodeType.Attribute:
                                ((XAttribute)xobj).Value = value;
                                found = true;
                                break;
                            case System.Xml.XmlNodeType.CDATA:
                            case System.Xml.XmlNodeType.Text:
                                ((XText)xobj).Value = value;
                                found = true;
                                break;
                            case System.Xml.XmlNodeType.Comment:
                                ((XComment)xobj).Value = value;
                                found = true;
                                break;
                            case System.Xml.XmlNodeType.Document:
                                doc = XDocument.Parse(value);
                                found = true;
                                break;
                            case System.Xml.XmlNodeType.Element:
                                ((XElement)xobj).ReplaceWith(XElement.Parse(value));
                                found = true;
                                break;
                            default:
                                break;
                        }
                    }
                }
                if (found)
                {
                    doc.Save(m_path);
                    return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
                }
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        }

        private HttpResponseMessage HandleDelete(string path, IXmlNamespaceResolver ns)
        {
            var doc = XDocument.Load(m_path);
            var result = doc.XPathEvaluate(path, ns);
            if (result is IEnumerable<object>)
            {
                foreach (var item in (IEnumerable<object>)result)
                {
                    var xobj = item as XObject;
                    if (xobj != null)
                    {
                        switch (xobj.NodeType)
                        {
                            case System.Xml.XmlNodeType.Attribute:
                                ((XAttribute)xobj).Remove();
                                break;
                            case System.Xml.XmlNodeType.CDATA:
                            case System.Xml.XmlNodeType.Text:
                                ((XText)xobj).Remove();
                                break;
                            case System.Xml.XmlNodeType.Comment:
                                ((XComment)xobj).Remove();
                                break;
                            case System.Xml.XmlNodeType.Document:
                                return new HttpResponseMessage(System.Net.HttpStatusCode.MethodNotAllowed);
                            case System.Xml.XmlNodeType.Element:
                                if (xobj == doc.Root)
                                    return new HttpResponseMessage(System.Net.HttpStatusCode.MethodNotAllowed);
                                else
                                    ((XElement)xobj).Remove();
                                break;
                            default:
                                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
                        }
                    }
                }
                doc.Save(m_path);
                return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
            }
            else
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            }
        }
    }
}