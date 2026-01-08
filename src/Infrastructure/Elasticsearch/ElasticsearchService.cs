using FIAP.CloudGames.Jogo.API.Domain.Entities;
using Nest;

namespace FIAP.CloudGames.Jogo.API.Infrastructure.Elasticsearch;

public interface IElasticsearchService
{
    Task IndexGameAsync(Game game);
    Task UpdateGameAsync(Game game);
    Task<IEnumerable<Game>> SearchGamesAsync(string query, string? genre = null, string? platform = null, decimal? minPrice = null, decimal? maxPrice = null, int page = 1, int pageSize = 10);
    Task<IEnumerable<Game>> GetPopularGamesAsync(int count = 10);
    Task<IEnumerable<Game>> GetRecommendationsAsync(Guid userId, int count = 10);
    Task<Dictionary<string, long>> GetGamesByGenreAggregationAsync();
    Task<Dictionary<string, long>> GetGamesByPlatformAggregationAsync();
}

public class ElasticsearchService : IElasticsearchService
{
    private readonly IElasticClient _elasticClient;
    private readonly ILogger<ElasticsearchService> _logger;
    private const string IndexName = "games";

    public ElasticsearchService(IElasticClient elasticClient, ILogger<ElasticsearchService> logger)
    {
        _elasticClient = elasticClient;
        _logger = logger;
        InitializeIndexAsync().Wait();
    }

    private async Task InitializeIndexAsync()
    {
        var indexExists = await _elasticClient.Indices.ExistsAsync(IndexName);
        if (!indexExists.Exists)
        {
            var createIndexResponse = await _elasticClient.Indices.CreateAsync(IndexName, c => c
                .Map<GameDocument>(m => m.AutoMap())
            );

            if (createIndexResponse.IsValid)
            {
                _logger.LogInformation("Índice {IndexName} criado com sucesso no Elasticsearch", IndexName);
            }
            else
            {
                _logger.LogError("Erro ao criar índice {IndexName}: {Error}", IndexName, createIndexResponse.DebugInformation);
            }
        }
    }

    public async Task IndexGameAsync(Game game)
    {
        var document = MapToDocument(game);
        var response = await _elasticClient.IndexAsync(document, idx => idx.Index(IndexName));
        
        if (response.IsValid)
        {
            _logger.LogInformation("Jogo {GameId} indexado com sucesso no Elasticsearch", game.Id);
        }
        else
        {
            _logger.LogError("Erro ao indexar jogo {GameId}: {Error}", game.Id, response.DebugInformation);
        }
    }

    public async Task UpdateGameAsync(Game game)
    {
        var document = MapToDocument(game);
        var response = await _elasticClient.UpdateAsync<GameDocument>(game.Id, u => u
            .Index(IndexName)
            .Doc(document)
            .DocAsUpsert(true)
        );
        
        if (response.IsValid)
        {
            _logger.LogInformation("Jogo {GameId} atualizado com sucesso no Elasticsearch", game.Id);
        }
        else
        {
            _logger.LogError("Erro ao atualizar jogo {GameId}: {Error}", game.Id, response.DebugInformation);
        }
    }

    public async Task<IEnumerable<Game>> SearchGamesAsync(string query, string? genre = null, string? platform = null, decimal? minPrice = null, decimal? maxPrice = null, int page = 1, int pageSize = 10)
    {
        var searchResponse = await _elasticClient.SearchAsync<GameDocument>(s => s
            .Index(IndexName)
            .From((page - 1) * pageSize)
            .Size(pageSize)
            .Query(q => q
                .Bool(b =>
                {
                    var must = new List<Func<QueryContainerDescriptor<GameDocument>, QueryContainer>>();

                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        must.Add(m => m.MultiMatch(mm => mm
                            .Fields(f => f
                                .Field(fd => fd.Title, boost: 2.0)
                                .Field(fd => fd.Description)
                            )
                            .Query(query)
                            .Fuzziness(Fuzziness.Auto)
                        ));
                    }

                    if (!string.IsNullOrWhiteSpace(genre))
                    {
                        must.Add(m => m.Term(t => t.Field(f => f.Genre).Value(genre)));
                    }

                    if (!string.IsNullOrWhiteSpace(platform))
                    {
                        must.Add(m => m.Term(t => t.Field(f => f.Platform).Value(platform)));
                    }

                    if (minPrice.HasValue || maxPrice.HasValue)
                    {
                        must.Add(m => m.Range(r =>
                        {
                            var rangeQuery = r.Field(f => f.Price);
                            if (minPrice.HasValue) rangeQuery = rangeQuery.GreaterThanOrEquals((double)minPrice.Value);
                            if (maxPrice.HasValue) rangeQuery = rangeQuery.LessThanOrEquals((double)maxPrice.Value);
                            return rangeQuery;
                        }));
                    }

                    return b.Must(must.ToArray());
                })
            )
        );

        if (searchResponse.IsValid)
        {
            return searchResponse.Documents.Select(MapToEntity);
        }

        _logger.LogError("Erro ao buscar jogos: {Error}", searchResponse.DebugInformation);
        return Enumerable.Empty<Game>();
    }

    public async Task<IEnumerable<Game>> GetPopularGamesAsync(int count = 10)
    {
        var searchResponse = await _elasticClient.SearchAsync<GameDocument>(s => s
            .Index(IndexName)
            .Size(count)
            .Query(q => q.MatchAll())
            .Sort(sort => sort
                .Descending(f => f.PurchaseCount)
                .Descending(f => f.ViewCount)
            )
        );

        if (searchResponse.IsValid)
        {
            return searchResponse.Documents.Select(MapToEntity);
        }

        _logger.LogError("Erro ao buscar jogos populares: {Error}", searchResponse.DebugInformation);
        return Enumerable.Empty<Game>();
    }

    public async Task<IEnumerable<Game>> GetRecommendationsAsync(Guid userId, int count = 10)
    {
        // Implementação simplificada de recomendação
        // Em produção, isso seria baseado no histórico do usuário
        return await GetPopularGamesAsync(count);
    }

    public async Task<Dictionary<string, long>> GetGamesByGenreAggregationAsync()
    {
        var searchResponse = await _elasticClient.SearchAsync<GameDocument>(s => s
            .Index(IndexName)
            .Size(0)
            .Aggregations(a => a
                .Terms("genres", t => t
                    .Field(f => f.Genre)
                    .Size(50)
                )
            )
        );

        if (searchResponse.IsValid)
        {
            var genresAgg = searchResponse.Aggregations.Terms("genres");
            return genresAgg.Buckets.ToDictionary(b => b.Key, b => b.DocCount ?? 0);
        }

        _logger.LogError("Erro ao agregar jogos por gênero: {Error}", searchResponse.DebugInformation);
        return new Dictionary<string, long>();
    }

    public async Task<Dictionary<string, long>> GetGamesByPlatformAggregationAsync()
    {
        var searchResponse = await _elasticClient.SearchAsync<GameDocument>(s => s
            .Index(IndexName)
            .Size(0)
            .Aggregations(a => a
                .Terms("platforms", t => t
                    .Field(f => f.Platform)
                    .Size(50)
                )
            )
        );

        if (searchResponse.IsValid)
        {
            var platformsAgg = searchResponse.Aggregations.Terms("platforms");
            return platformsAgg.Buckets.ToDictionary(b => b.Key, b => b.DocCount ?? 0);
        }

        _logger.LogError("Erro ao agregar jogos por plataforma: {Error}", searchResponse.DebugInformation);
        return new Dictionary<string, long>();
    }

    private GameDocument MapToDocument(Game game)
    {
        return new GameDocument
        {
            Id = game.Id,
            Title = game.Title,
            Description = game.Description,
            Price = game.Price,
            ImageUrl = game.ImageUrl,
            Genre = game.Genre,
            Platform = game.Platform,
            ViewCount = game.ViewCount,
            PurchaseCount = game.PurchaseCount,
            CreatedAt = game.CreatedAt,
            UpdatedAt = game.UpdatedAt
        };
    }

    private Game MapToEntity(GameDocument document)
    {
        var game = new Game(
            document.Title,
            document.Description,
            document.Price,
            document.ImageUrl,
            document.Genre,
            document.Platform
        );

        // Usar reflection para definir valores privados
        typeof(Game).GetProperty(nameof(Game.Id))?.SetValue(game, document.Id);
        typeof(Game).GetProperty(nameof(Game.ViewCount))?.SetValue(game, document.ViewCount);
        typeof(Game).GetProperty(nameof(Game.PurchaseCount))?.SetValue(game, document.PurchaseCount);
        typeof(Game).GetProperty(nameof(Game.CreatedAt))?.SetValue(game, document.CreatedAt);
        typeof(Game).GetProperty(nameof(Game.UpdatedAt))?.SetValue(game, document.UpdatedAt);

        return game;
    }
}
