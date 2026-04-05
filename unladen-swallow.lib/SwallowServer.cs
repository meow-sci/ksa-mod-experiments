using System;
using System.Net;
using System.Threading.Tasks;
using GenHTTP.Api.Content;
using GenHTTP.Api.Infrastructure;
using GenHTTP.Api.Protocol;
using GenHTTP.Engine.Internal;
using GenHTTP.Modules.ErrorHandling;
using GenHTTP.Modules.Functional;
using GenHTTP.Modules.IO;
using GenHTTP.Modules.Layouting;
using GenHTTP.Modules.Layouting.Provider;
using GenHTTP.Modules.Practices;
using GenHTTP.Modules.Security;

namespace MeowSci.UnladenSwallowLib;

/// <summary>
/// Embedded GenHTTP server that exposes KSA mod functionality over HTTP.
/// Listens on 0.0.0.0:7887.
/// </summary>
public sealed class SwallowServer
{
    private const string BindHost = "0.0.0.0";
    private const ushort Port = 7887;

    private IServerHost? _host;

    public bool IsRunning => _host is not null;

    public async Task StartAsync()
    {
        if (_host is not null) return;

        var api = Layout.Create();
        RegisterRoutes(api);

        _host = await Host.Create()
                          .Handler(api)
                          .Bind(IPAddress.Parse(BindHost), Port)
                          .Defaults(compression: false)
                          .Console()
                          .Development()
                          .StartAsync();

        Console.WriteLine($"unladen-swallow: server listening on http://{BindHost}:{Port}");
    }

    public async Task StopAsync()
    {
        if (_host is null) return;

        await _host.StopAsync();
        _host = null;

        Console.WriteLine("unladen-swallow: server stopped.");
    }

    // ── Route registry ────────────────────────────────────────────────────────
    // All endpoints are registered here. Add new routes to this method.

    private static void RegisterRoutes(LayoutBuilder api)
    {
        // GET /health
        api.Add("health", Inline.Create().Get(() => new { status = "ok" }));

        // GET /fov, POST /fov
        api.Add("fov", FovEndpoint.Create());

        // POST /vehicle/actions/ignite
        // POST /vehicle/actions/shutdown
        var vehicleActions = Layout.Create()
            .Add("ignite", ActionIgnite.Create())
            .Add("shutdown", ActionShutdown.Create());

        api.Add("vehicle", Layout.Create()
            .Add("actions", vehicleActions));

        // GET  /blinky/grids
        // POST /blinky/animate
        // POST /blinky/static
        // POST /blinky/off
        api.Add("blinky", Layout.Create()
            .Add("grids", BlinkyListEndpoint.Create())
            .Add("animate", BlinkyAnimateEndpoint.Create())
            .Add("static", BlinkyStaticEndpoint.Create())
            .Add("off", BlinkyOffEndpoint.Create()));

        // GET    /camera/status
        // POST   /camera/animate
        // DELETE /camera/stop
        api.Add("camera", Layout.Create()
            .Add("status", CameraStatusEndpoint.Create())
            .Add("animate", CameraAnimateEndpoint.Create())
            .Add("stop", CameraStopEndpoint.Create()));

        // CORS — allow all origins
        api.Add(CorsPolicy.Permissive());

        // JSON error responses
        api.Add(ErrorHandler.From(new JsonErrorMapper()));
    }

    // ── Error handling ────────────────────────────────────────────────────────

    private sealed class JsonErrorMapper : IErrorMapper<Exception>
    {
        public ValueTask<IResponse?> GetNotFound(IRequest request, IHandler handler)
        {
            var response = request.Respond()
                                  .Status(ResponseStatus.NotFound)
                                  .Content("{\"status\":\"error\",\"message\":\"not found\"}")
                                  .Type(FlexibleContentType.Get(ContentType.ApplicationJson))
                                  .Build();
            return new ValueTask<IResponse?>(response);
        }

        public ValueTask<IResponse?> Map(IRequest request, IHandler handler, Exception error)
        {
            Console.WriteLine($"unladen-swallow: unhandled exception in request: {error}");

            var status = error is ProviderException pe
                ? pe.Status
                : ResponseStatus.InternalServerError;

            var escaped = error.Message.Replace("\\", "\\\\").Replace("\"", "\\\"");
            var response = request.Respond()
                                  .Status(status)
                                  .Content($"{{\"status\":\"error\",\"message\":\"{escaped}\"}}")
                                  .Type(FlexibleContentType.Get(ContentType.ApplicationJson))
                                  .Build();
            return new ValueTask<IResponse?>(response);
        }
    }
}
