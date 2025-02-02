﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using NalogZaUtovar;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
string applicationName = "Nalog Utovar";

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Logging.ClearProviders(); // Uklanja postojeće log providere
builder.Logging.AddSerilog(); // Dodaje Serilog kao logger

builder.Host.UseSerilog(); // Postavlja Serilog kao default logger
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("ApplicationName", applicationName)// Čita postavke iz appsettings.json// Seq URL // Dodaje kontekst logovanja
    .CreateLogger();


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

builder.Services.AddSingleton<DocumentService>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    string connectionString = configuration.GetConnectionString("OracleBaza");
    return new DocumentService(connectionString);
});

var app = builder.Build();

app.UseCors("AllowAll");
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.MapPost("/get-pdf", async ([FromBody] NalogDto requestBody, HttpContext context, IMemoryCache cache, DocumentService documentService) =>
{
    Log.Information("Primljen zahtev za PDF sa parametrima: {RequestBody}", requestBody);

    if (requestBody != null &&
        !string.IsNullOrWhiteSpace(requestBody.Skladiste) &&
        !string.IsNullOrWhiteSpace(requestBody.Kupac) &&
        !string.IsNullOrWhiteSpace(requestBody.Kamion))
    {
        string cacheKey = $"{requestBody.Skladiste}-{requestBody.Kupac}-{requestBody.Kamion}-{requestBody.Nalog}";

        if (!cache.TryGetValue(cacheKey, out byte[]? cachedBlob))
        {
            cachedBlob = await documentService.GetDocumentByDetailsAsync(
                requestBody.Skladiste, requestBody.Kupac, requestBody.Kamion, requestBody.Nalog);

            if (cachedBlob == null)
            {
                Log.Warning("Dokument nije pronađen za ključ: {CacheKey}", cacheKey);
                return Results.NotFound("Dokument nije pronađen.");
            }

            cache.Set(cacheKey, cachedBlob, TimeSpan.FromMinutes(10));
        }

        Log.Information("Šaljem PDF za ključ: {CacheKey}", cacheKey);

        context.Response.ContentType = "application/pdf";
        await context.Response.Body.WriteAsync(cachedBlob);
        return Results.Empty;
    }

    Log.Warning("Neispravni parametri: {RequestBody}", requestBody);
    return Results.BadRequest("Svi parametri (Skladiste, Kupac, Kamion, Nalog) su obavezni.");
});

app.MapPost("/pdfPoNalogu", async ([FromBody] BrojNalogaDto requestBody ,HttpContext context, IMemoryCache cache, DocumentService documentService) =>
{
    string cacheKey = $"order-{requestBody.BrojNaloga}";

    if (!cache.TryGetValue(cacheKey, out byte[]? cachedBlob))
    {
        cachedBlob = await documentService.GetDocumentByOrderNumberAsync(requestBody.BrojNaloga);

        if (cachedBlob == null)
            return Results.NotFound("Dokument nije pronađen.");

        cache.Set(cacheKey, cachedBlob, TimeSpan.FromMinutes(10));
    }

    context.Response.ContentType = "application/pdf";
    await context.Response.Body.WriteAsync(cachedBlob);

    Log.Information("Uspesno vracen pdf fajl za broj naloga: ", requestBody.BrojNaloga);

    return Results.Empty;
});

app.MapGet("/skladista", async (DocumentService documentService, IMemoryCache cache) =>
{
    const string cacheKey = "skladista";

    // Proveri da li su skladišta već u kešu
    if (!cache.TryGetValue(cacheKey, out List<string> skladista))
    {
        // Ako nisu u kešu, učitaj iz baze
        skladista = await documentService.GetAllSkladista();

        // Dodaj u keš sa vremenskim istekom od 30 dana VRATI OPET NA TO
        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        };

        cache.Set(cacheKey, skladista, cacheEntryOptions);
    }

    return Results.Ok(skladista);
});

app.MapGet("/kupciPoSkladistu", async (string skladiste, DocumentService documentService, IMemoryCache cache) =>
{
    var cacheKey = $"kupci-{skladiste}";
    if (!cache.TryGetValue(cacheKey, out List<string> kupci))
    {
        kupci = await documentService.GetKupciPoSkladistu(skladiste);

        // Postavljanje kesiranja (30 minuta)
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        };
        cache.Set(cacheKey, kupci, cacheOptions);
    }

    return Results.Ok(kupci);
})
.WithName("Kupci po skladistu")
.Produces<List<string>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithOpenApi(options =>
{
    options.Description = "Vraća sve kupce za određeno skladište.";
    options.Parameters[0].Description = "Naziv skladišta za koje želite preuzeti kupce.";
    return options;
});

app.MapGet("/kamioniPoSkladistuIKupcu", async (string skladiste, string kupac, DocumentService documentService, IMemoryCache cache) =>
{
    // Kreiranje ključa za kesiranje
    var cacheKey = $"kamioni-{skladiste}-{kupac}";
    if (!cache.TryGetValue(cacheKey, out List<string> kamioni))
    {
        kamioni = await documentService.GetKamioniPoSkladistuIKupcu(skladiste, kupac);

        // Postavljanje kesiranja na 30 minuta
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        };
        cache.Set(cacheKey, kamioni, cacheOptions);
    }

    if (kamioni == null || !kamioni.Any())
        return Results.NotFound("Nema kamiona za dato skladiste i kupca.");

    return Results.Ok(kamioni);
})
.WithName("Kamioni Po Skladistu i Kupcu")
.Produces<List<string>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithOpenApi(options =>
{
    options.Description = "Vraća sve kamione za određeno skladište.";
    options.Parameters[0].Description = "Naziv skladišta";
    options.Parameters[1].Description = "Naziv kupca";
    return options;
});

app.MapGet("/naloziPoSkladistuKupcuKamionu", async (string skladiste, string kupac, string kamion, DocumentService documentService, IMemoryCache cache, ILogger<Program> logger) =>
{
    // Kreiranje ključa za kesiranje
    var cacheKey = $"nalozi-{skladiste}-{kupac}-{kamion}";
    if (!cache.TryGetValue(cacheKey, out List<int> nalozi))
    {
        nalozi = await documentService.GetNaloziPoSkladistuKupcuKamionu(skladiste, kupac, kamion);

        // Postavljanje kesiranja na 30 minuta
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        };
        cache.Set(cacheKey, nalozi, cacheOptions);
    }

    if (nalozi == null || !nalozi.Any())
    {
        Log.Warning($"Nema naloga za dato skladiste,kupca i kamion. {skladiste}, {kupac}, {kamion}");
        return Results.NotFound("Nema naloga za dato skladiste,kupca i kamion.");
    }
    Log.Information($"Nalozi po skladistu: {nalozi.ToString}");
    return Results.Ok(nalozi);
})
.WithName("Nalozi Po Skladistu,Kupcu i Kamionu")
.Produces<List<string>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithOpenApi(options =>
{
    options.Description = "Vraća sve kupce za određeno skladište.";
    options.Parameters[0].Description = "Naziv skladišta";
    options.Parameters[0].Description = "Naziv kupca";
    options.Parameters[0].Description = "Registracija kamiona";
    return options;
});

app.MapPost("/insert", async (
    [FromBody] NalogPostDto request,
    DocumentService documentService) =>
{
    if (request.Dokument == null)
        return Results.BadRequest("Dokument je obavezan.");
    if (request.Skladiste == null)
        return Results.BadRequest("Skladiste je obavezno");
    if (request.Kupac == null)
        return Results.BadRequest("Kupac je obavezan");
    if (request.BrojNaloga == null)
        return Results.BadRequest("Broj naloga je obavezan");
    if (request.RegistracijaVozila == null)
        return Results.BadRequest("Registracija vozila je obavezna");

    await documentService.PostNalog(request);

    return Results.Ok("Red uspešno dodat.");
})
.WithTags("Unos naloga")
.WithName("Unos jednog naloga")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.WithOpenApi(options =>
{
    options.Description = "Ubacuje novi red u tabelu.";
    return options;
});

app.MapPost("/multiinsert", async (
    [FromBody] InsertUpdateDto requestDto,
    DocumentService documentService) =>
{
    if(requestDto.RequestListInsert != null && requestDto.RequestListInsert.Count != 0)
    {
        foreach (var request in requestDto.RequestListInsert)
        {
            if (request.Dokument == null)
                return Results.BadRequest("Dokument je obavezan.");
            if (request.Skladiste == null)
                return Results.BadRequest("Skladiste je obavezno");
            if (request.Kupac == null)
                return Results.BadRequest("Kupac je obavezan");
            if (request.BrojNaloga == null)
                return Results.BadRequest("Broj naloga je obavezan");
            if (request.RegistracijaVozila == null)
                return Results.BadRequest("Registracija vozila je obavezna");
        }
    }
    if(requestDto.RequestListUpdate != null && requestDto.RequestListUpdate.Count != 0)
    {
        foreach (var request in requestDto.RequestListUpdate)
        {
            if (request.Dokument == null)
                return Results.BadRequest("Dokument je obavezan.");
            if (request.Skladiste == null)
                return Results.BadRequest("Skladiste je obavezno");
            if (request.Kupac == null)
                return Results.BadRequest("Kupac je obavezan");
            if (request.BrojNaloga == null)
                return Results.BadRequest("Broj naloga je obavezan");
            if (request.RegistracijaVozila == null)
                return Results.BadRequest("Registracija vozila je obavezna");
        }
    }

    await documentService.PostMultipleNalozi(requestDto.RequestListInsert, requestDto.RequestListUpdate);

    Log.Information($"Uspešno upisano {requestDto.RequestListInsert?.Count ?? 0} naloga u bazu! " +
        $"Uspešno azurirano {requestDto.RequestListUpdate?.Count ?? 0} naloga u bazi!");

    return Results.Ok($"Uspešno upisano {requestDto.RequestListInsert?.Count ?? 0} naloga u bazu! " +
        $"Uspešno azurirano {requestDto.RequestListUpdate?.Count ?? 0} naloga u bazi!");
})
.WithTags("Unos naloga")
.WithName("Ubacivanje i azuriranje vise naloga odjednom")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.WithOpenApi(options =>
{
    options.Description = "Ubacuje ili azurira vise naloga u tabeli.";
    return options;
});





app.Run();
