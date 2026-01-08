namespace FIAP.CloudGames.Jogo.API.Domain.Events;

public record GameCreatedEvent(
    Guid GameId,
    string Title,
    string Description,
    decimal Price,
    string? ImageUrl,
    string? Genre,
    string? Platform,
    DateTime CreatedAt
);

public record GameUpdatedEvent(
    Guid GameId,
    string Title,
    string Description,
    decimal Price,
    DateTime UpdatedAt
);

public record GameViewedEvent(
    Guid GameId,
    string Title,
    DateTime ViewedAt
);

public record GameSearchedEvent(
    string SearchQuery,
    int ResultsCount,
    DateTime SearchedAt
);
