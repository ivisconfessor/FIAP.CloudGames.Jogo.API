using System.ComponentModel.DataAnnotations;

namespace FIAP.CloudGames.Jogo.API.Domain.Entities;

public class Game
{
    public Guid Id { get; private set; }
    
    [Required]
    [StringLength(100)]
    public string Title { get; private set; }
    
    [Required]
    public string Description { get; private set; }
    
    [Required]
    public decimal Price { get; private set; }
    
    public string? ImageUrl { get; private set; }
    
    public string? Genre { get; private set; }
    
    public string? Platform { get; private set; }
    
    public int ViewCount { get; private set; }
    
    public int PurchaseCount { get; private set; }
    
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Game() { } // Para o EF Core

    public Game(string title, string description, decimal price, string? imageUrl = null, string? genre = null, string? platform = null)
    {
        Id = Guid.NewGuid();
        Title = title;
        Description = description;
        Price = price;
        ImageUrl = imageUrl;
        Genre = genre;
        Platform = platform;
        ViewCount = 0;
        PurchaseCount = 0;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(string title, string description, decimal price, string? imageUrl = null, string? genre = null, string? platform = null)
    {
        Title = title;
        Description = description;
        Price = price;
        ImageUrl = imageUrl;
        Genre = genre;
        Platform = platform;
        UpdatedAt = DateTime.UtcNow;
    }

    public void IncrementViewCount()
    {
        ViewCount++;
    }

    public void IncrementPurchaseCount()
    {
        PurchaseCount++;
    }
}
