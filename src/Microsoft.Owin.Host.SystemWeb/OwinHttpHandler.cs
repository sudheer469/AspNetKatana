﻿// <copyright file="OwinHttpHandler.cs" company="Katana contributors">
//   Copyright 2011-2012 Katana contributors
// </copyright>
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using System.Web.Routing;

// ReSharper disable AccessToModifiedClosure

namespace Microsoft.Owin.Host.SystemWeb
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class OwinHttpHandler : IHttpAsyncHandler
    {
        private readonly string _pathBase;
        private readonly Func<AppFunc> _appAccessor;

        public OwinHttpHandler()
        {
            _pathBase = Utils.NormalizePath(HttpRuntime.AppDomainAppVirtualPath);
        }

        public OwinHttpHandler(string pathBase, AppFunc app)
            : this(pathBase, () => app)
        {
        }

        public OwinHttpHandler(string pathBase, Func<AppFunc> appAccessor)
        {
            _pathBase = pathBase;
            _appAccessor = appAccessor;
        }

        // REVIEW: public properties here are extremely bad. overload ctor instead.
        public RequestContext RequestContext { get; set; }
        public string RequestPath { get; set; }

        public bool IsReusable
        {
            get { return true; }
        }

        void IHttpHandler.ProcessRequest(HttpContext context)
        {
            ProcessRequest(new HttpContextWrapper(context));
        }

        public void ProcessRequest(HttpContextBase context)
        {
            // the synchronous version of this handler must never be called
            throw new NotImplementedException();
        }

        IAsyncResult IHttpAsyncHandler.BeginProcessRequest(HttpContext context, AsyncCallback cb, object extraData)
        {
            return BeginProcessRequest(new HttpContextWrapper(context), cb, extraData);
        }

        public IAsyncResult BeginProcessRequest(HttpContextBase httpContext, AsyncCallback cb, object extraData)
        {
            // REVIEW: the httpContext.Request.RequestContext may be used here if public property unassigned?

            var callContext = new OwinCallContext(cb, extraData);
            try
            {
                var requestContext = RequestContext ?? new RequestContext(httpContext, new RouteData());
                var requestPathBase = _pathBase;
                var requestPath = RequestPath ?? httpContext.Request.AppRelativeCurrentExecutionFilePath.Substring(1) + httpContext.Request.PathInfo;

                var app = _appAccessor.Invoke();
                if (app == null)
                {
                    throw new NullReferenceException("OwinHttpHandler cannot invoke a null app delegate");
                }

                callContext.Execute(requestContext, requestPathBase, requestPath, app);
            }
            catch (Exception ex)
            {
                callContext.Complete(ex);
                callContext.Dispose();
            }
            return callContext;
        }

        public void EndProcessRequest(IAsyncResult result)
        {
            OwinCallContext.End(result);
            ((OwinCallContext)result).Dispose();
        }
    }
}