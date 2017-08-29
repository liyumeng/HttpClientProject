using HttpClientUI.Models;
using HttpClientUI.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace HttpClientUI.ViewModels
{
    class HttpClientMainViewModel : NotificationObject
    {
        private static readonly string DefaultUserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)";
        public HttpClientMainViewModel()
        {
            SubmitCommand = new ActionCommand();
            SubmitCommand.Executing += SubmitCommand_Executing;
            SelectedMethod = MethodSource.First();
            UrlContent = "http://www.baidu.com";
            StatusColor = 0;
            StatusInfo = "就绪";
            RequestHeaders = new ObservableCollection<HeaderModel>();
            ResponseHeaders = new ObservableCollection<HeaderModel>();
            RequestBodyType = BodyType.RAW;
            ResponseBodyType = BodyType.HTML;
            UpdateRequestHeader("User-Agent", DefaultUserAgent);
        }

        private void SubmitCommand_Executing(object sender, ExecutingEventArgs e)
        {
            ClearResponseContent();
            HttpWebRequest request;
            if (UrlContent.StartsWith("https", StringComparison.OrdinalIgnoreCase))
            {
                ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(CheckValidationResult);
                request = WebRequest.Create(UrlContent) as HttpWebRequest;
                request.ProtocolVersion = HttpVersion.Version10;
            }
            else
            {
                request = WebRequest.Create(UrlContent) as HttpWebRequest;
            }


            request.Method = SelectedMethod.Name;

            foreach (var header in RequestHeaders)
            {
                if (String.IsNullOrWhiteSpace(header.Key) == false && String.IsNullOrWhiteSpace(header.Value) == false)
                    request.SetRawHeader(header.Key, header.Value);
            }



            if (SelectedMethod.Id == 2)
            {
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(RequestBodyContent);
                request.ContentLength = bytes.Length;
                Stream requestStream = request.GetRequestStream();
                requestStream.Write(bytes, 0, bytes.Length);
                requestStream.Close();
            }

            submitRequest(request);
        }


        private async void submitRequest(HttpWebRequest request)
        {
            System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            HttpStatusCode statusCode = HttpStatusCode.NotFound;
            Waiting();
            HttpWebResponse response;
            bool exception = false;
            try
            {
                response = (await request.GetResponseAsync()) as HttpWebResponse;

                using (var s = response.GetResponseStream())
                {
                    using (var reader = new StreamReader(s, Encoding.UTF8))
                    {
                        var content = reader.ReadToEnd();
                        m_responseBodyContentRaw = content;
                        string contentType = response.ContentType;
                        ResponseBodyType = BodyType.RAW;
                        if (contentType.IndexOf("application/json") > -1 || contentType.IndexOf("text/json") > -1)
                        {
                            ResponseBodyType = BodyType.JSON;
                        }
                        else if (contentType.IndexOf("application/xml") > -1 || contentType.IndexOf("text/xml") > -1)
                        {
                            ResponseBodyType = BodyType.XML;
                        }
                        else
                        {
                            ResponseBodyType = BodyType.HTML;
                        }
                    }
                }

                statusCode = response.StatusCode;
                UpdateResponseHeaders(response);
                response.Close();
            }
            catch (WebException ex)
            {
                response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    using (var s = ex.Response.GetResponseStream())
                    {
                        using (var reader = new StreamReader(s, Encoding.UTF8))
                        {
                            m_responseBodyContentRaw = reader.ReadToEnd();
                            ResponseBodyType = BodyType.JSON;
                        }
                    }
                    statusCode = (ex.Response as HttpWebResponse).StatusCode;
                }
                else
                {
                    m_responseBodyContentRaw = ex.Message;
                    BodyContent = ex.Message;
                    exception = true;
                }
            }

            timer.Stop();
            if (statusCode == HttpStatusCode.OK)
                StatusColor = 0;
            else
                StatusColor = 2;
            if (exception)
            {
                StatusInfo = String.Format("请求异常,  耗时：{1} ms", timer.ElapsedMilliseconds);
            }
            else
            {
                long size = response.ContentLength;
                if (size == -1)
                {
                    size = System.Text.Encoding.ASCII.GetBytes(m_bodyContent).LongLength;
                }
                StatusInfo = String.Format("状态: {0} {1},  耗时：{2} ms, 返回结果大小：{3}", (int)statusCode, statusCode.ToString(), timer.ElapsedMilliseconds, size);
            }
        }


        private void ClearResponseContent()
        {
            BodyContent = "";
            ResponseHeaders.Clear();
        }
        private void UpdateResponseHeaders(HttpWebResponse response)
        {
            ResponseHeaders.Clear();
            foreach (string key in response.Headers)
            {
                ResponseHeaders.Add(new HeaderModel(key, response.Headers[key]));
            }
        }

        private void Waiting()
        {
            StatusColor = 1;
            StatusInfo = "请等待......";
        }
        private string toHtml(string content)
        {
            Regex re = new Regex("(\r*\n[ \t\r\n]*\n){1,}", RegexOptions.Compiled);
            content = re.Replace(content, "\n");
            return content;
        }

        private string toJson(string content)
        {
            try
            {
                //格式化json字符串
                JsonSerializer serializer = new JsonSerializer();
                TextReader tr = new StringReader(content);
                JsonTextReader jtr = new JsonTextReader(tr);
                object obj = serializer.Deserialize(jtr);
                if (obj != null)
                {
                    StringWriter textWriter = new StringWriter();
                    JsonTextWriter jsonWriter = new JsonTextWriter(textWriter)
                    {
                        Formatting = Newtonsoft.Json.Formatting.Indented,
                        Indentation = 4,
                        IndentChar = ' '
                    };
                    serializer.Serialize(jsonWriter, obj);
                    return textWriter.ToString();
                }
                else
                {
                    return content;
                }
            }
            catch (Exception e)
            {
                content = e.Message;
            }
            return content;
        }

        private string toXml(string content)
        {
            try
            {
                XmlDocument xd = new XmlDocument();
                xd.LoadXml(content);
                StringBuilder sb = new StringBuilder();
                StringWriter sw = new StringWriter(sb);
                XmlTextWriter xtw = null;
                try
                {
                    xtw = new XmlTextWriter(sw);

                    xtw.Formatting = System.Xml.Formatting.Indented;
                    xtw.Indentation = 4;
                    xtw.IndentChar = ' ';

                    xd.WriteTo(xtw);
                }
                finally
                {
                    if (xtw != null)
                        xtw.Close();
                }
                return sb.ToString();
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        private static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            return true; //总是接受  
        }

        public ActionCommand SubmitCommand { get; set; }


        private BodyType m_responseBodyType;
        public BodyType ResponseBodyType
        {
            get { return m_responseBodyType; }
            set
            {
                if (value != m_responseBodyType)
                {
                    m_responseBodyType = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("ResponseBodyType"));

                    switch (value)
                    {
                        case BodyType.HTML: BodyContent = toHtml(m_responseBodyContentRaw); break;
                        case BodyType.JSON: BodyContent = toJson(m_responseBodyContentRaw); break;
                        case BodyType.XML: BodyContent = toXml(m_responseBodyContentRaw); break;
                        case BodyType.RAW: BodyContent = m_responseBodyContentRaw; break;
                        default: break;
                    }
                }
            }
        }

        private BodyType m_requestBodyType;
        public BodyType RequestBodyType
        {
            get { return m_requestBodyType; }
            set
            {
                if (value != m_requestBodyType)
                {
                    m_requestBodyType = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("RequestBodyType"));
                    if (m_requestBodyType == BodyType.JSON || m_requestBodyType == BodyType.XML)
                    {
                        string v = "application/json";
                        if (m_requestBodyType == BodyType.XML)
                            v = "text/xml; encoding='utf-8'";
                        UpdateRequestHeader("Content-Type", v);
                    }
                }
            }
        }


        private int m_statusColor;

        public int StatusColor
        {
            get { return m_statusColor; }
            set
            {
                if (value != m_statusColor)
                {
                    m_statusColor = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("StatusColor"));
                }
            }
        }

        private string m_statusInfo;
        /// <summary>
        /// 状态栏信息
        /// </summary>
        public string StatusInfo
        {
            get { return m_statusInfo; }
            set
            {
                if (value != m_statusInfo)
                {
                    m_statusInfo = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("StatusInfo"));
                }
            }
        }

        private string m_requestBodyContent = "";
        public string RequestBodyContent
        {
            get { return m_requestBodyContent; }
            set
            {
                if (value != m_requestBodyContent)
                {
                    m_requestBodyContent = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("RequestBodyContent"));
                }
            }
        }

        private string m_responseBodyContentRaw = "";
        private string m_bodyContent;
        /// <summary>
        /// 请求返回的Body信息
        /// </summary>
        public string BodyContent
        {
            get
            {
                return m_bodyContent;
            }
            set
            {
                if (value != m_bodyContent)
                {
                    m_bodyContent = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("BodyContent"));
                }
            }
        }

        private string m_urlContent;
        /// <summary>
        /// 输入的URL信息
        /// </summary>
        public string UrlContent
        {
            get
            {
                return m_urlContent;
            }
            set
            {
                if (value != m_urlContent)
                {
                    m_urlContent = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("UrlContent"));
                }
            }
        }

        private ObservableCollection<MethodModel> m_methodSource = null;
        public ObservableCollection<MethodModel> MethodSource
        {
            get
            {
                if (this.m_methodSource == null)
                {
                    this.m_methodSource = new ObservableCollection<MethodModel>
                    {
                        new MethodModel() { Id=1,Name="GET"},
                        new MethodModel() { Id=2,Name="POST"}
                    };
                }
                return this.m_methodSource;
            }
        }

        private MethodModel m_selectedMethod;
        public MethodModel SelectedMethod
        {
            get
            {
                return this.m_selectedMethod;
            }
            set
            {
                if (value != m_selectedMethod)
                {
                    m_selectedMethod = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("SelectedMethod"));
                }
            }
        }

        private ObservableCollection<HeaderModel> m_requestHeaders;
        public ObservableCollection<HeaderModel> RequestHeaders
        {
            get
            {
                return this.m_requestHeaders;
            }
            set
            {
                if (value != m_requestHeaders)
                {
                    m_requestHeaders = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("RequestHeaders"));
                }
            }
        }

        public void UpdateRequestHeader(string key, string value)
        {

            foreach (var header in m_requestHeaders)
            {
                if (header.Key == key)
                {
                    header.Value = value;
                    return;
                }
            }
            RequestHeaders.Add(new HeaderModel(key, value));
        }


        private ObservableCollection<HeaderModel> m_responseHeaders;
        public ObservableCollection<HeaderModel> ResponseHeaders
        {
            get
            {
                return this.m_responseHeaders;
            }
            set
            {
                if (value != m_responseHeaders)
                {
                    m_responseHeaders = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("ResponseHeaders"));
                }
            }
        }
    }
}
