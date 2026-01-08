using Nest;

namespace FIAP.CloudGames.Jogo.API.Infrastructure.Elasticsearch;

[ElasticsearchType(IdProperty = nameof(Id))]
public class GameDocument
{
    [Keyword]
    public Guid Id { get; set; }
    
    [Text(Analyzer = "standard")]
    public string Title { get; set; } = string.Empty;
    
    [Text(Analyzer = "standard")]
    public string Description { get; set; } = string.Empty;
    
    [Number(NumberType.Double)]
    public decimal Price { get; set; }
    
    [Keyword]
    public string? ImageUrl { get; set; }
    
    [Keyword]
    public string? Genre { get; set; }
    
    [Keyword]
    public string? Platform { get; set; }
    
    [Number(NumberType.Integer)]
    public int ViewCount { get; set; }
    
    [Number(NumberType.Integer)]
    public int PurchaseCount { get; set; }
    
    [Date]
    public DateTime CreatedAt { get; set; }
    
    [Date]
    public DateTime? UpdatedAt { get; set; }
}
