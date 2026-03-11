using TransitRealtime;
using Google.Protobuf;
using ALVA.EDISONAPI;
using ALWA.SYS;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();

var app = builder.Build();

var config = builder.Configuration.GetSection("CacheConnection");

var mlRest = new MLRest(
    config["Server"]!,
    int.Parse(config["Port"]!),
    config["Namespace"]!,
    config["Idfa"]!)
{
    LoginName = config["LoginName"]!,
    FullName = config["FullName"]!
};

// --- Debug endpointy pro vraceni vysledku v JSON formatu ---
app.MapGet("/vehiclepositions.json", async (HttpContext ctx) =>
{
    string json = GetFeedJson(mlRest, GtfsFeedType.VehiclePositions);
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsync(json);
});

app.MapGet("/tripupdates.json", async (HttpContext ctx) =>
{
    string json = GetFeedJson(mlRest, GtfsFeedType.TripUpdates);
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsync(json);
});

// --- Produkcni endpointy pro vraceni vysledku v protobuf formatu ---
app.MapGet("/vehiclepositions.pb", async (HttpContext ctx) =>
{
    try
    {
        string json = GetFeedJson(mlRest, GtfsFeedType.VehiclePositions);
        var feed = FeedMessage.Parser.ParseJson(json);
        ctx.Response.ContentType = "application/octet-stream";
        feed.WriteTo(ctx.Response.BodyWriter.AsStream());
    }
    catch (Exception ex)
    {
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsync("ERROR: " + ex.Message);
    }
});

app.MapGet("/tripupdates.pb", async (HttpContext ctx) =>
{
    try
    {
        string json = GetFeedJson(mlRest, GtfsFeedType.TripUpdates);
        var feed = FeedMessage.Parser.ParseJson(json);
        ctx.Response.ContentType = "application/octet-stream";
        feed.WriteTo(ctx.Response.BodyWriter.AsStream());
    }
    catch (Exception ex)
    {
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsync("ERROR: " + ex.Message);
    }
});

app.Run();
static string GetFeedJson(MLRest mlRest, GtfsFeedType feedType)
{
    string method = feedType switch
    {
        GtfsFeedType.VehiclePositions => "GetVehiclePositions",
        GtfsFeedType.TripUpdates => "GetTripUpdates",
        _ => throw new ArgumentOutOfRangeException(nameof(feedType))
    };

    CVar result = mlRest.Query("ALVA.MHD2.Linky.API.Export", method, new CVar());

    // check if MLRest call failed
    if (mlRest.RestStatus.State != MLRStatus.EState.Ok)
        throw new Exception($"MLRest error: {mlRest.RestStatus.Message}");

    var chunksNode = result[method + "Chunks"];
    if (chunksNode?.Value == null)
        throw new Exception($"No data returned from Caché for method {method}");

    int chunks = int.Parse(chunksNode.Value.ToString());
    var sb = new StringBuilder();
    for (int i = 1; i <= chunks; i++)
        sb.Append(result[method][i.ToString()].Value.ToString());

    return sb.ToString();
}

enum GtfsFeedType
{
    VehiclePositions,
    TripUpdates
}