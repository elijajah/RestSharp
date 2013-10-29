#region License
//   Copyright 2010 John Sheehan
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//	 http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License. 
#endregion

using System;
using System.Threading;
#if NET4 || WP8 || MONODROID || MONOTOUCH
using System.Threading.Tasks;
#endif

namespace RestSharp
{
    public partial class RestClient
    {
        /// <summary>
        /// Executes the request and callback asynchronously, authenticating if needed
        /// </summary>
        /// <param name="request">Request to be executed</param>
        public virtual async Task<IRestResponse> ExecuteAsync(IRestRequest request)
        {

            string method = Enum.GetName(typeof(Method), request.Method);
            switch (request.Method)
            {
                case Method.PATCH:
                case Method.POST:
                case Method.PUT:
                    return await ExecuteAsync(request, method, DoAsPostAsync).ConfigureAwait(false);
                default:
                    return await ExecuteAsync(request, method, DoAsGetAsync).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Executes a GET-style request and callback asynchronously, authenticating if needed
        /// </summary>
        /// <param name="request">Request to be executed</param>
        /// <param name="httpMethod">The HTTP method to execute</param>
        public virtual async Task<IRestResponse> ExecuteAsyncGet(IRestRequest request, string httpMethod)
        {
            return await ExecuteAsync(request, httpMethod, DoAsGetAsync).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a POST-style request and callback asynchronously, authenticating if needed
        /// </summary>
        /// <param name="request">Request to be executed</param>
        /// <param name="httpMethod">The HTTP method to execute</param>
        public virtual async Task<IRestResponse> ExecuteAsyncPost(IRestRequest request, string httpMethod)
        {
            request.Method = Method.POST;  // Required by RestClient.BuildUri... 
            return await ExecuteAsync(request, httpMethod, DoAsPostAsync).ConfigureAwait(false);
        }

        private async Task<IRestResponse> ExecuteAsync(IRestRequest request, string httpMethod, Func<IHttp, string, Task<HttpResponse>> getWebRequest)
        {
            var http = HttpFactory.Create();
            AuthenticateIfNeeded(this, request);

            ConfigureHttp(request, http);
            var response = await getWebRequest(http, httpMethod).ConfigureAwait(false);
            return ProcessResponse(request, response);
        }

        private async static Task<HttpResponse> DoAsGetAsync(IHttp http, string method)
        {
            return await http.AsGetAsync(method).ConfigureAwait(false);
        }

        private async static Task<HttpResponse> DoAsPostAsync(IHttp http, string method)
        {
            return await http.AsPostAsync(method).ConfigureAwait(false);
        }

        private IRestResponse ProcessResponse(IRestRequest request, HttpResponse httpResponse)
        {
            return ConvertToRestResponse(request, httpResponse);
        }

        /// <summary>
        /// Executes the request and callback asynchronously, authenticating if needed
        /// </summary>
        /// <typeparam name="T">Target deserialization type</typeparam>
        /// <param name="request">Request to be executed</param>
        public virtual async Task<IRestResponse<T>> ExecuteAsync<T>(IRestRequest request)
        {
            var response = await ExecuteAsync(request).ConfigureAwait(false);
            return DeserializeResponse<T>(request, response);
        }

        /// <summary>
        /// Executes a GET-style request and callback asynchronously, authenticating if needed
        /// </summary>
        /// <typeparam name="T">Target deserialization type</typeparam>
        /// <param name="request">Request to be executed</param>
        /// <param name="httpMethod">The HTTP method to execute</param>
        public virtual async Task<IRestResponse<T>> ExecuteAsyncGet<T>(IRestRequest request, string httpMethod)
        {
            var response = await ExecuteAsyncGet(request, httpMethod).ConfigureAwait(false);
            return DeserializeResponse<T>(request, response);
        }

        /// <summary>
        /// Executes a POST-style request and callback asynchronously, authenticating if needed
        /// </summary>
        /// <typeparam name="T">Target deserialization type</typeparam>
        /// <param name="request">Request to be executed</param>
        /// <param name="httpMethod">The HTTP method to execute</param>
        public virtual async Task<IRestResponse<T>> ExecuteAsyncPost<T>(IRestRequest request, string httpMethod)
        {
            var response = await ExecuteAsyncPost(request, httpMethod).ConfigureAwait(false);
            return DeserializeResponse<T>(request, response);
        }

        private IRestResponse<T> DeserializeResponse<T>(IRestRequest request, IRestResponse response)
        {
            IRestResponse<T> restResponse = response as RestResponse<T>;
            if (response.ResponseStatus == ResponseStatus.Completed)
            {
                restResponse = Deserialize<T>(request, response);
            }
            return restResponse;
        }

#if NET4 || WP8 || MONODROID || MONOTOUCH
        /// <summary>
        /// Executes a GET-style request asynchronously, authenticating if needed
        /// </summary>
        /// <typeparam name="T">Target deserialization type</typeparam>
        /// <param name="request">Request to be executed</param>
        public virtual async Task<IRestResponse<T>> ExecuteGetTaskAsync<T>(IRestRequest request)
        {
            return await ExecuteGetTaskAsync<T>(request, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a GET-style request asynchronously, authenticating if needed
        /// </summary>
        /// <typeparam name="T">Target deserialization type</typeparam>
        /// <param name="request">Request to be executed</param>
        /// <param name="token">The cancellation token</param>
        public virtual async Task<IRestResponse<T>> ExecuteGetTaskAsync<T>(IRestRequest request, CancellationToken token)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            request.Method = Method.GET;
            return await ExecuteTaskAsync<T>(request, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a POST-style request asynchronously, authenticating if needed
        /// </summary>
        /// <typeparam name="T">Target deserialization type</typeparam>
        /// <param name="request">Request to be executed</param>
        public virtual async Task<IRestResponse<T>> ExecutePostTaskAsync<T>(IRestRequest request)
        {
            return await ExecutePostTaskAsync<T>(request, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a POST-style request asynchronously, authenticating if needed
        /// </summary>
        /// <typeparam name="T">Target deserialization type</typeparam>
        /// <param name="request">Request to be executed</param>
        /// <param name="token">The cancellation token</param>
        public virtual async Task<IRestResponse<T>> ExecutePostTaskAsync<T>(IRestRequest request, CancellationToken token)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            request.Method = Method.POST;
            return await ExecuteTaskAsync<T>(request, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes the request asynchronously, authenticating if needed
        /// </summary>
        /// <typeparam name="T">Target deserialization type</typeparam>
        /// <param name="request">Request to be executed</param>
        public virtual async Task<IRestResponse<T>> ExecuteTaskAsync<T>(IRestRequest request)
        {
            return await ExecuteTaskAsync<T>(request, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes the request asynchronously, authenticating if needed
        /// </summary>
        /// <typeparam name="T">Target deserialization type</typeparam>
        /// <param name="request">Request to be executed</param>
        /// <param name="token">The cancellation token</param>
        public virtual async Task<IRestResponse<T>> ExecuteTaskAsync<T>(IRestRequest request, CancellationToken token)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            return await ExecuteAsync<T>(request).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes the request asynchronously, authenticating if needed
        /// </summary>
        /// <param name="request">Request to be executed</param>
        public virtual async Task<IRestResponse> ExecuteTaskAsync(IRestRequest request)
        {
            return await ExecuteTaskAsync(request, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a GET-style asynchronously, authenticating if needed
        /// </summary>
        /// <param name="request">Request to be executed</param>
        public virtual async Task<IRestResponse> ExecuteGetTaskAsync(IRestRequest request)
        {
            return await ExecuteGetTaskAsync(request, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a GET-style asynchronously, authenticating if needed
        /// </summary>
        /// <param name="request">Request to be executed</param>
        /// <param name="token">The cancellation token</param>
        public virtual async Task<IRestResponse> ExecuteGetTaskAsync(IRestRequest request, CancellationToken token)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            request.Method = Method.GET;
            return await ExecuteTaskAsync(request, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a POST-style asynchronously, authenticating if needed
        /// </summary>
        /// <param name="request">Request to be executed</param>
        public virtual async Task<IRestResponse> ExecutePostTaskAsync(IRestRequest request)
        {
            return await ExecutePostTaskAsync(request, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a POST-style asynchronously, authenticating if needed
        /// </summary>
        /// <param name="request">Request to be executed</param>
        /// <param name="token">The cancellation token</param>
        public virtual async Task<IRestResponse> ExecutePostTaskAsync(IRestRequest request, CancellationToken token)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            request.Method = Method.POST;
            return await ExecuteTaskAsync(request, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes the request asynchronously, authenticating if needed
        /// </summary>
        /// <param name="request">Request to be executed</param>
        /// <param name="token">The cancellation token</param>
        public virtual async Task<IRestResponse> ExecuteTaskAsync(IRestRequest request, CancellationToken token)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            return await ExecuteAsync(request).ConfigureAwait(false);
        }
#endif
    }
}