#region License
//   Copyright 2010 John Sheehan
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License. 
#endregion

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using RestSharp.Extensions;

#if SILVERLIGHT
using System.Windows.Browser;
using System.Net.Browser;
#endif

#if WINDOWS_PHONE
using System.Windows.Threading;
using System.Windows;
#endif

#if (FRAMEWORK && !MONOTOUCH && !MONODROID)
using System.Web;
#endif

namespace RestSharp
{
    /// <summary>
    /// HttpWebRequest wrapper (async methods)
    /// </summary>
    public partial class Http
    {
        private TimeOutState _timeoutState;

        public async Task<HttpResponse> DeleteAsync()
        {
            return await GetStyleMethodInternalAsync("DELETE").ConfigureAwait(false);
        }

        public async Task<HttpResponse> GetAsync()
        {
            return await GetStyleMethodInternalAsync("GET").ConfigureAwait(false);
        }

        public async Task<HttpResponse> HeadAsync()
        {
            return await GetStyleMethodInternalAsync("HEAD").ConfigureAwait(false);
        }

        public async Task<HttpResponse> OptionsAsync()
        {
            return await GetStyleMethodInternalAsync("OPTIONS").ConfigureAwait(false);
        }

        public async Task<HttpResponse> PostAsync()
        {
            return await PutPostInternalAsync("POST").ConfigureAwait(false);
        }

        public async Task<HttpResponse> PutAsync()
        {
            return await PutPostInternalAsync("PUT").ConfigureAwait(false);
        }

        public async Task<HttpResponse> PatchAsync()
        {
            return await PutPostInternalAsync("PATCH").ConfigureAwait(false);
        }

        /// <summary>
        /// Execute an async POST-style request with the specified HTTP Method.  
        /// </summary>
        /// <param name="httpMethod">The HTTP method to execute.</param>
        /// <returns></returns>
        public async Task<HttpResponse> AsPostAsync(string httpMethod)
        {
            return await PutPostInternalAsync(httpMethod.ToUpperInvariant()).ConfigureAwait(false);
        }

        /// <summary>
        /// Execute an async GET-style request with the specified HTTP Method.  
        /// </summary>
        /// <param name="httpMethod">The HTTP method to execute.</param>
        /// <returns></returns>
        public async Task<HttpResponse> AsGetAsync(string httpMethod)
        {
            return await GetStyleMethodInternalAsync(httpMethod.ToUpperInvariant()).ConfigureAwait(false);
        }

        private async Task<HttpResponse> GetStyleMethodInternalAsync(string method)
        {
            try
            {
                var url = Url;
                HttpWebRequest webRequest = ConfigureAsyncWebRequest(method, url);
                _timeoutState = new TimeOutState { Request = webRequest };
                return await Task.Factory.FromAsync(webRequest.BeginGetResponse, result => ResponseCallback(result), webRequest).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return CreateErrorResponse(ex);
            }
        }

        private HttpResponse CreateErrorResponse(Exception ex)
        {
            var response = new HttpResponse();
            var webException = ex as WebException;
            if (webException != null && webException.Status == WebExceptionStatus.RequestCanceled)
            {
                response.ResponseStatus = _timeoutState.TimedOut ? ResponseStatus.TimedOut : ResponseStatus.Aborted;
                return response;
            }

            response.ErrorMessage = ex.Message;
            response.ErrorException = ex;
            response.ResponseStatus = ResponseStatus.Error;
            return response;
        }

        private async Task<HttpResponse> PutPostInternalAsync(string method)
        {
            HttpWebRequest webRequest = null;
            try
            {
                webRequest = ConfigureAsyncWebRequest(method, Url);
                PreparePostBody(webRequest);
                return await WriteRequestBodyAsync(webRequest).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return CreateErrorResponse(ex);
            }
        }

        private async Task<HttpResponse> WriteRequestBodyAsync(HttpWebRequest webRequest)
        {
            IAsyncResult asyncResult;
            _timeoutState = new TimeOutState { Request = webRequest };

            if (HasBody || HasFiles || AlwaysMultipartFormData)
            {
#if !WINDOWS_PHONE || WP8
                webRequest.ContentLength = CalculateContentLength();
#endif

                return await Task.Factory.FromAsync(webRequest.BeginGetRequestStream, result => RequestStreamCallback(result).Result, webRequest).ConfigureAwait(false);
            }

            else
            {
                webRequest.AllowReadStreamBuffering = false;
                var task = Task.Factory.FromAsync<HttpWebResponse>(webRequest.BeginGetResponse, r => (HttpWebResponse)webRequest.EndGetResponse(r), null);
                try
                {
                    var httpWebResponse = await task.ContinueWith(
   t =>
   {
       if (t.Exception == null)
           return t.Result;

       // Not the best way to fault "continuation" task
       // but you can wrap this into your special exception
       // and add original exception as a inner exception
       throw t.Exception.InnerException;

       // throw CustomException("The request failed!", t.Exception.InnerException);
   }).ConfigureAwait(false);
                    return ExtractResponseData(httpWebResponse);
                }
                catch (Exception)
                {
                    throw;
                }
            }

            SetTimeout(asyncResult, _timeoutState);
        }

        private long CalculateContentLength()
        {
            if (RequestBodyBytes != null)
                return RequestBodyBytes.Length;

            if (!HasFiles && !AlwaysMultipartFormData)
            {
                return _defaultEncoding.GetByteCount(RequestBody);
            }

            // calculate length for multipart form
            long length = 0;
            foreach (var file in Files)
            {
                length += _defaultEncoding.GetByteCount(GetMultipartFileHeader(file));
                length += file.ContentLength;
                length += _defaultEncoding.GetByteCount(_lineBreak);
            }

            foreach (var param in Parameters)
            {
                length += _defaultEncoding.GetByteCount(GetMultipartFormData(param));
            }

            length += _defaultEncoding.GetByteCount(GetMultipartFooter());
            return length;
        }

        private async Task<HttpResponse> RequestStreamCallback(IAsyncResult result)
        {
            var webRequest = (HttpWebRequest)result.AsyncState;

            if (_timeoutState.TimedOut)
            {
                var response = new HttpResponse { ResponseStatus = ResponseStatus.TimedOut };
                return response;
            }

            // write body to request stream
            try
            {
                using (var requestStream = webRequest.EndGetRequestStream(result))
                {
                    if (HasFiles || AlwaysMultipartFormData)
                    {
                        WriteMultipartFormData(requestStream);
                    }
                    else if (RequestBodyBytes != null)
                    {
                        requestStream.Write(RequestBodyBytes, 0, RequestBodyBytes.Length);
                    }
                    else
                    {
                        WriteStringTo(requestStream, RequestBody);
                    }
                }
            }
            catch (Exception ex)
            {
                return CreateErrorResponse(ex);
            }
            return await Task.Factory.FromAsync(webRequest.BeginGetResponse, r => ResponseCallback(r), webRequest).ConfigureAwait(false);
        }

        private void SetTimeout(IAsyncResult asyncResult, TimeOutState timeOutState)
        {
#if FRAMEWORK || WP8
            if (Timeout != 0)
            {
                ThreadPool.RegisterWaitForSingleObject(asyncResult.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), timeOutState, Timeout, true);
            }
#endif
        }

        private static void TimeoutCallback(object state, bool timedOut)
        {
            if (!timedOut)
                return;

            var timeoutState = state as TimeOutState;

            if (timeoutState == null)
            {
                return;
            }

            lock (timeoutState)
            {
                timeoutState.TimedOut = true;
            }

            if (timeoutState.Request != null)
            {
                timeoutState.Request.Abort();
            }
        }

        private static HttpWebResponse GetRawResponseAsync(IAsyncResult result)
        {
            HttpWebResponse raw = null;

            try
            {
                var webRequest = (HttpWebRequest)result.AsyncState;
                raw = webRequest.EndGetResponse(result) as HttpWebResponse;
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.RequestCanceled)
                {
                    throw ex;
                }

                // Check to see if this is an HTTP error or a transport error.
                // In cases where an HTTP error occurs ( status code >= 400 )
                // return the underlying HTTP response, otherwise assume a
                // transport exception (ex: connection timeout) and
                // rethrow the exception

                if (ex.Response is HttpWebResponse)
                {
                    raw = ex.Response as HttpWebResponse;
                }
                else
                {
                    throw ex;
                }
            }
            finally
            {
                if (raw != null)
                    raw.Close();
            }
            return raw;
        }

        private HttpResponse ResponseCallback(IAsyncResult result)
        {
            var response = new HttpResponse { ResponseStatus = ResponseStatus.None };

            try
            {
                if (_timeoutState.TimedOut)
                {
                    response.ResponseStatus = ResponseStatus.TimedOut;
                    return response;
                }

                var raw = GetRawResponseAsync(result);
                return ExtractResponseData(raw);
            }
            catch (Exception ex)
            {
                return CreateErrorResponse(ex);
            }
        }

        private static void ExecuteCallback(HttpResponse response, Action<HttpResponse> callback)
        {
            callback(response);
        }

        partial void AddAsyncHeaderActions()
        {
#if SILVERLIGHT
			_restrictedHeaderActions.Add("Content-Length", (r, v) => r.ContentLength = Convert.ToInt64(v));
#endif
#if WINDOWS_PHONE && !WP8
			// WP7 doesn't as of Beta doesn't support a way to set Content-Length either directly
			// or indirectly
			_restrictedHeaderActions.Add("Content-Length", (r, v) => { });
#endif
        }

        private HttpWebRequest ConfigureAsyncWebRequest(string method, Uri url)
        {
#if SILVERLIGHT
			WebRequest.RegisterPrefix("http://", WebRequestCreator.ClientHttp);
			WebRequest.RegisterPrefix("https://", WebRequestCreator.ClientHttp);
#endif
            var webRequest = (HttpWebRequest)WebRequest.Create(url);
            webRequest.UseDefaultCredentials = false;

            AppendHeaders(webRequest);
            AppendCookies(webRequest);

            webRequest.Method = method;

            // make sure Content-Length header is always sent since default is -1
#if !WINDOWS_PHONE || WP8
            // WP7 doesn't as of Beta doesn't support a way to set this value either directly
            // or indirectly
            if (!HasFiles && !AlwaysMultipartFormData)
            {
                webRequest.ContentLength = 0;
            }
#endif

            if (Credentials != null)
            {
                webRequest.Credentials = Credentials;
            }

#if !SILVERLIGHT
            if (UserAgent.HasValue())
            {
                webRequest.UserAgent = UserAgent;
            }
#endif

#if FRAMEWORK
			if(ClientCertificates != null)
			{
				webRequest.ClientCertificates.AddRange(ClientCertificates);
			}
			
			webRequest.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip | DecompressionMethods.None;
			ServicePointManager.Expect100Continue = false;

			if (Timeout != 0)
			{
				webRequest.Timeout = Timeout;
			}

			if (Proxy != null)
			{
				webRequest.Proxy = Proxy;
			}

			if (FollowRedirects && MaxRedirects.HasValue)
			{
				webRequest.MaximumAutomaticRedirections = MaxRedirects.Value;
			}
#endif

#if !SILVERLIGHT
            webRequest.AllowAutoRedirect = FollowRedirects;
#endif
            return webRequest;
        }

        private class TimeOutState
        {
            public bool TimedOut { get; set; }
            public HttpWebRequest Request { get; set; }
        }
    }
}
