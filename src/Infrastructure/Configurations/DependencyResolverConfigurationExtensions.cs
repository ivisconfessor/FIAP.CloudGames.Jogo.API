using System.Text;
using FIAP.CloudGames.Jogo.API.Infrastructure.Data;
using FIAP.CloudGames.Jogo.API.Infrastructure.EventSourcing;
using FIAP.CloudGames.Jogo.API.Infrastructure.Elasticsearch;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Nest;
using Elasticsearch.Net;

namespace FIAP.CloudGames.Jogo.API.Infrastructure.Configurations;

public static class DependencyResolverConfigurationExtensions
{
    public static void IntegrateDependencyResolver(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuração do DbContext
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("FIAPCloudGamesJogos"));

        // Configuração do Elasticsearch
        var elasticsearchUrl = configuration["Elasticsearch:Url"] ?? "http://localhost:9200";
        var connectionSettings = new ConnectionSettings(new Uri(elasticsearchUrl))
            .DefaultIndex("games")
            .EnableDebugMode()
            .PrettyJson()
            .RequestTimeout(TimeSpan.FromMinutes(2));

        var client = new ElasticClient(connectionSettings);
        services.AddSingleton<IElasticClient>(client);

        // Configuração do JWT
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(configuration["Jwt:Key"]))
            };
        });

        // Configuração da Autorização
        services.AddAuthorization();

        // Registro dos serviços
        services.AddScoped<IElasticsearchService, ElasticsearchService>();
        services.AddSingleton<IEventStore, EventStore>();
        
        // CORS
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            });
        });
    }
}
