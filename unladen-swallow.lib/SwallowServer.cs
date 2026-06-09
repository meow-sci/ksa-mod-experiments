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

        // GET/POST/DELETE /blinky/grids
        // POST            /blinky/grids/scan
        // POST            /blinky/grids/scan-all
        // POST/DELETE     /blinky/animate
        // POST            /blinky/animate/builtin
        // POST            /blinky/static
        // POST            /blinky/pattern
        // POST            /blinky/off
        // GET/POST        /blinky/render
        // POST            /blinky/engines/deactivate
        api.Add("blinky", Layout.Create()
            .Add("grids", Layout.Create()
                .Add(BlinkyListEndpoint.Create())
                .Add(BlinkyGridsEndpoint.Create())
                .Add("scan", BlinkyGridScanEndpoint.Create())
                .Add("scan-all", BlinkyGridScanAllEndpoint.Create()))
            .Add("animate", Layout.Create()
                .Add(BlinkyAnimateEndpoint.Create())
                .Add("builtin", BlinkyBuiltInScrollEndpoint.Create()))
            .Add("static", BlinkyStaticEndpoint.Create())
            .Add("pattern", BlinkyPatternEndpoint.Create())
            .Add("off", BlinkyOffEndpoint.Create())
            .Add("render", BlinkyRenderEndpoint.Create())
            .Add("engines", Layout.Create()
                .Add("deactivate", BlinkyEngineDeactivateEndpoint.Create())));

        // GET/POST/DELETE /shiny/grids
        // POST            /shiny/grids/scan
        // POST            /shiny/grids/scan-all
        // POST/DELETE     /shiny/animate
        // POST            /shiny/static
        // POST            /shiny/pattern
        // POST            /shiny/off
        // GET/POST        /shiny/appearance
        api.Add("shiny", Layout.Create()
            .Add("grids", Layout.Create()
                .Add(ShinyListEndpoint.Create())
                .Add(ShinyGridsEndpoint.Create())
                .Add("scan", ShinyGridScanEndpoint.Create())
                .Add("scan-all", ShinyGridScanAllEndpoint.Create()))
            .Add("animate", ShinyAnimateEndpoint.Create())
            .Add("static", ShinyStaticEndpoint.Create())
            .Add("pattern", ShinyPatternEndpoint.Create())
            .Add("off", ShinyOffEndpoint.Create())
            .Add("appearance", ShinyAppearanceEndpoint.Create()));

        // GET    /camera/status
        // POST   /camera/animate
        // DELETE /camera/stop
        api.Add("camera", Layout.Create()
            .Add("status", CameraStatusEndpoint.Create())
            .Add("animate", CameraAnimateEndpoint.Create())
            .Add("stop", CameraStopEndpoint.Create()));

        // GET    /torch/welds           — list active welds
        // POST   /torch/welds           — create a new weld
        // DELETE /torch/welds           — unweld (remove a weld)
        // POST   /torch/welds/modify    — modify weld immediately
        // POST   /torch/welds/animate   — animate weld to target state
        // GET    /torch/presets         — list all presets
        // POST   /torch/presets         — save/update a preset
        // DELETE /torch/presets         — delete a preset
        api.Add("torch", Layout.Create()
            .Add("welds", Layout.Create()
                .Add(TorchWeldsEndpoint.Create())
                .Add("modify", TorchWeldModifyEndpoint.Create())
                .Add("animate", TorchWeldAnimateEndpoint.Create()))
            .Add("presets", TorchPresetsEndpoint.Create()));

        // GET    /zippo/lights          — list light parts on a vehicle
        // POST   /zippo/lights/state    — set color/intensity/enabled
        // POST   /zippo/animate         — queue light animation
        // DELETE /zippo/animate         — clear animation queue
        api.Add("zippo", Layout.Create()
            .Add("lights", Layout.Create()
                .Add(ZippoLightsEndpoint.Create())
                .Add("state", ZippoLightStateEndpoint.Create()))
            .Add("animate", ZippoAnimateEndpoint.Create()));

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
