using System.ComponentModel.DataAnnotations;

namespace FIAP.CloudGames.Jogo.API.Application.DTOs;

public record CreateGameDto(
    [Required] [StringLength(100)] string Title,
    [Required] string Description,
    [Required] [Range(0, double.MaxValue)] decimal Price,
    string? ImageUrl,
    string? Genre,
    string? Platform);

public record UpdateGameDto(
    [Required] [StringLength(100)] string Title,
    [Required] string Description,
    [Required] [Range(0, double.MaxValue)] decimal Price,
    string? ImageUrl,
    string? Genre,
    string? Platform);

public record GameResponseDto(
    Guid Id,
    string Title,
    string Description,
    decimal Price,
    string? ImageUrl,
    string? Genre,
    string? Platform,
    int ViewCount,
    int PurchaseCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record GameSearchDto(
    string Query,
    string? Genre,
    string? Platform,
    decimal? MinPrice,
    decimal? MaxPrice,
    int Page = 1,
    int PageSize = 10);
