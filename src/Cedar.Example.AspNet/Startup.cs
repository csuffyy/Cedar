﻿using Cedar.Example.AspNet;
using Microsoft.Owin;

[assembly: OwinStartup(typeof (Startup))]

namespace Cedar.Example.AspNet
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using Cedar.Handlers;
    using Cedar.Queries;

    using Owin;

    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            var context = new OwinContext(app.Properties);
            var token = context.Get<CancellationToken>("host.OnAppDisposing");
            if (token != CancellationToken.None)
            {
                token.Register(() => App.Instance.Dispose());
            }

            app.Use(App.Instance.CommandingMiddleWare);
            app.Use(App.Instance.QueryingMiddleWare);
        }
    }
}