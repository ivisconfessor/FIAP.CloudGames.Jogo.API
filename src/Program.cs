using System.Security.Claims;
using FIAP.CloudGames.Jogo.API.Domain.Entities;
using FIAP.CloudGames.Jogo.API.Domain.Events;
using FIAP.CloudGames.Jogo.API.Application.DTOs;
using FIAP.CloudGames.Jogo.API.Infrastructure.Configurations;
using FIAP.CloudGames.Jogo.API.Infrastructure.Data;
using FIAP.CloudGames.Jogo.API.Infrastructure.EventSourcing;
using FIAP.CloudGames.Jogo.API.Infrastructure.Elasticsearch;
using FIAP.CloudGames.Jogo.API.Application.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using FluentValidation.AspNetCore;
using FluentValidation;
using FIAP.CloudGames.Jogo.API.Application.Validators;
using Serilog;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Usar Serilog
builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "FIAP Cloud Games - Jogos API", 
        Version = "v1",
        Description = "Microsserviço de gerenciamento de jogos com Elasticsearch para busca avançada"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Registrar FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateGameDtoValidator>();

// Integrar todas as dependências
builder.Services.IntegrateDependencyResolver(builder.Configuration);

// Configurar Application Insights
builder.Services.AddApplicationInsightsTelemetry();

// Configurar OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("FIAP.CloudGames.Jogo.API"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter();
    });

var app = builder.Build();

// Seed de jogos iniciais para demonstração
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var elasticsearchService = scope.ServiceProvider.GetRequiredService<IElasticsearchService>();
    
    if (!db.Games.Any())
    {
        var games = new[]
        {
            new Game("The Witcher 3", "RPG épico de mundo aberto", 149.90m, "https://example.com/witcher3.jpg", "RPG", "PC"),
            new Game("Cyberpunk 2077", "RPG futurista em Night City", 199.90m, "https://example.com/cyberpunk.jpg", "RPG", "PC"),
            new Game("FIFA 24", "Simulador de futebol", 299.90m, "https://example.com/fifa24.jpg", "Esporte", "PlayStation"),
            new Game("Call of Duty", "Shooter em primeira pessoa", 249.90m, "https://example.com/cod.jpg", "FPS", "Xbox"),
            new Game("Minecraft", "Jogo de construção e sobrevivência", 79.90m, "https://example.com/minecraft.jpg", "Sandbox", "PC")
        };
        
        db.Games.AddRange(games);
        db.SaveChanges();

        // Indexar no Elasticsearch
        foreach (var game in games)
        {
            await elasticsearchService.IndexGameAsync(game);
        }
        
        Log.Information("Jogos iniciais criados e indexados com sucesso");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FIAP Cloud Games - Jogos API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// Game endpoints
app.MapPost("/api/games", async (CreateGameDto dto, ApplicationDbContext db, IElasticsearchService elasticsearchService, IEventStore eventStore, ClaimsPrincipal user, ILogger<Program> logger) =>
{
    logger.LogInformation("Criando novo jogo: {Title}", dto.Title);
    
    var userRole = user.FindFirst(ClaimTypes.Role)?.Value;
    if (userRole != "Admin")
    {
        logger.LogWarning("Tentativa de criar jogo sem permissão de admin");
        return Results.Forbid();
    }

    var game = new Game(dto.Title, dto.Description, dto.Price, dto.ImageUrl, dto.Genre, dto.Platform);
    db.Games.Add(game);
    await db.SaveChangesAsync();

    // Indexar no Elasticsearch
    await elasticsearchService.IndexGameAsync(game);

    // Event Sourcing
    var gameCreatedEvent = new GameCreatedEvent(
        game.Id, 
        game.Title, 
        game.Description, 
        game.Price, 
        game.ImageUrl,
        game.Genre,
        game.Platform,
        game.CreatedAt
    );
    await eventStore.SaveEventAsync(gameCreatedEvent);

    logger.LogInformation("Jogo criado com sucesso. ID: {GameId}", game.Id);

    return Results.Created($"/api/games/{game.Id}", new GameResponseDto(
        game.Id, game.Title, game.Description, game.Price,
        game.ImageUrl, game.Genre, game.Platform, game.ViewCount, game.PurchaseCount,
        game.CreatedAt, game.UpdatedAt));
})
.RequireAuthorization()
.WithName("CreateGame")
.WithOpenApi();

app.MapGet("/api/games", async (ApplicationDbContext db, ILogger<Program> logger) =>
{
    logger.LogInformation("Listando todos os jogos");
    
    var games = await db.Games
        .Select(g => new GameResponseDto(
            g.Id, g.Title, g.Description, g.Price,
            g.ImageUrl, g.Genre, g.Platform, g.ViewCount, g.PurchaseCount,
            g.CreatedAt, g.UpdatedAt))
        .ToListAsync();

    return Results.Ok(games);
})
.RequireAuthorization()
.WithName("GetGames")
.WithOpenApi();

app.MapGet("/api/games/{id}", async (Guid id, ApplicationDbContext db, IEventStore eventStore, ILogger<Program> logger) =>
{
    logger.LogInformation("Buscando jogo com ID: {GameId}", id);
    
    var game = await db.Games.FindAsync(id);
    if (game == null)
    {
        logger.LogWarning("Jogo não encontrado. ID: {GameId}", id);
        return Results.NotFound("Jogo não encontrado");
    }

    // Incrementar visualização
    game.IncrementViewCount();
    await db.SaveChangesAsync();

    // Event Sourcing
    var gameViewedEvent = new GameViewedEvent(game.Id, game.Title, DateTime.UtcNow);
    await eventStore.SaveEventAsync(gameViewedEvent);

    return Results.Ok(new GameResponseDto(
        game.Id, game.Title, game.Description, game.Price,
        game.ImageUrl, game.Genre, game.Platform, game.ViewCount, game.PurchaseCount,
        game.CreatedAt, game.UpdatedAt));
})
.RequireAuthorization()
.WithName("GetGame")
.WithOpenApi();

app.MapPut("/api/games/{id}", async (Guid id, UpdateGameDto dto, ApplicationDbContext db, IElasticsearchService elasticsearchService, IEventStore eventStore, ClaimsPrincipal user, ILogger<Program> logger) =>
{
    logger.LogInformation("Atualizando jogo com ID: {GameId}", id);
    
    var userRole = user.FindFirst(ClaimTypes.Role)?.Value;
    if (userRole != "Admin")
    {
        logger.LogWarning("Tentativa de atualizar jogo sem permissão de admin");
        return Results.Forbid();
    }

    var game = await db.Games.FindAsync(id);
    if (game == null)
    {
        logger.LogWarning("Jogo não encontrado para atualização. ID: {GameId}", id);
        return Results.NotFound("Jogo não encontrado");
    }

    game.Update(dto.Title, dto.Description, dto.Price, dto.ImageUrl, dto.Genre, dto.Platform);
    await db.SaveChangesAsync();

    // Atualizar no Elasticsearch
    await elasticsearchService.UpdateGameAsync(game);

    // Event Sourcing
    var gameUpdatedEvent = new GameUpdatedEvent(
        game.Id,
        game.Title,
        game.Description,
        game.Price,
        game.UpdatedAt ?? DateTime.UtcNow
    );
    await eventStore.SaveEventAsync(gameUpdatedEvent);

    logger.LogInformation("Jogo atualizado com sucesso. ID: {GameId}", id);

    return Results.Ok(new GameResponseDto(
        game.Id, game.Title, game.Description, game.Price,
        game.ImageUrl, game.Genre, game.Platform, game.ViewCount, game.PurchaseCount,
        game.CreatedAt, game.UpdatedAt));
})
.RequireAuthorization()
.WithName("UpdateGame")
.WithOpenApi();

// Elasticsearch endpoints
app.MapPost("/api/games/search", async (GameSearchDto searchDto, IElasticsearchService elasticsearchService, IEventStore eventStore, ILogger<Program> logger) =>
{
    logger.LogInformation("Buscando jogos com query: {Query}", searchDto.Query);
    
    var games = await elasticsearchService.SearchGamesAsync(
        searchDto.Query,
        searchDto.Genre,
        searchDto.Platform,
        searchDto.MinPrice,
        searchDto.MaxPrice,
        searchDto.Page,
        searchDto.PageSize
    );

    var results = games.Select(g => new GameResponseDto(
        g.Id, g.Title, g.Description, g.Price,
        g.ImageUrl, g.Genre, g.Platform, g.ViewCount, g.PurchaseCount,
        g.CreatedAt, g.UpdatedAt
    )).ToList();

    // Event Sourcing
    var gameSearchedEvent = new GameSearchedEvent(
        searchDto.Query,
        results.Count,
        DateTime.UtcNow
    );
    await eventStore.SaveEventAsync(gameSearchedEvent);

    logger.LogInformation("Busca retornou {Count} resultados", results.Count);

    return Results.Ok(results);
})
.RequireAuthorization()
.WithName("SearchGames")
.WithOpenApi();

app.MapGet("/api/games/popular", async (int count, IElasticsearchService elasticsearchService, ILogger<Program> logger) =>
{
    logger.LogInformation("Buscando jogos populares. Count: {Count}", count);
    
    var games = await elasticsearchService.GetPopularGamesAsync(count);
    
    var results = games.Select(g => new GameResponseDto(
        g.Id, g.Title, g.Description, g.Price,
        g.ImageUrl, g.Genre, g.Platform, g.ViewCount, g.PurchaseCount,
        g.CreatedAt, g.UpdatedAt
    )).ToList();

    return Results.Ok(results);
})
.RequireAuthorization()
.WithName("GetPopularGames")
.WithOpenApi();

app.MapGet("/api/games/recommendations", async (ClaimsPrincipal user, IElasticsearchService elasticsearchService, ILogger<Program> logger) =>
{
    var userId = Guid.Parse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
    logger.LogInformation("Buscando recomendações para usuário: {UserId}", userId);
    
    var games = await elasticsearchService.GetRecommendationsAsync(userId, 10);
    
    var results = games.Select(g => new GameResponseDto(
        g.Id, g.Title, g.Description, g.Price,
        g.ImageUrl, g.Genre, g.Platform, g.ViewCount, g.PurchaseCount,
        g.CreatedAt, g.UpdatedAt
    )).ToList();

    return Results.Ok(results);
})
.RequireAuthorization()
.WithName("GetRecommendations")
.WithOpenApi();

app.MapGet("/api/games/analytics/by-genre", async (IElasticsearchService elasticsearchService, ILogger<Program> logger) =>
{
    logger.LogInformation("Buscando agregação de jogos por gênero");
    
    var aggregation = await elasticsearchService.GetGamesByGenreAggregationAsync();
    
    return Results.Ok(aggregation);
})
.RequireAuthorization()
.WithName("GetGamesByGenre")
.WithOpenApi();

app.MapGet("/api/games/analytics/by-platform", async (IElasticsearchService elasticsearchService, ILogger<Program> logger) =>
{
    logger.LogInformation("Buscando agregação de jogos por plataforma");
    
    var aggregation = await elasticsearchService.GetGamesByPlatformAggregationAsync();
    
    return Results.Ok(aggregation);
})
.RequireAuthorization()
.WithName("GetGamesByPlatform")
.WithOpenApi();

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", service = "jogos", timestamp = DateTime.UtcNow }))
.AllowAnonymous()
.WithName("HealthCheck")
.WithOpenApi();

app.MapGet("/api/events/{aggregateId}", async (Guid aggregateId, IEventStore eventStore, ILogger<Program> logger) =>
{
    logger.LogInformation("Buscando eventos para aggregate ID: {AggregateId}", aggregateId);
    
    var events = await eventStore.GetEventsAsync(aggregateId);
    return Results.Ok(events);
})
.RequireAuthorization()
.WithName("GetEvents")
.WithOpenApi();

Log.Information("Iniciando FIAP.CloudGames.Jogo.API...");

app.Urls.Add("http://*:8080");

app.Run();
