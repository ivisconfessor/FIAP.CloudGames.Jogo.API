using System.Text;
using FIAP.CloudGames.Jogo.API.Infrastructure.Data;
using FIAP.CloudGames.Jogo.API.Infrastructure.EventSourcing;
using FIAP.CloudGames.Jogo.API.Infrastructure.Elasticsearch;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Nest;

namespace FIAP.CloudGames.Jogo.API.Infrastructure.Configurations;

public static class DependencyResolverConfigurationExtensions
{
    public static void IntegrateDependencyResolver(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuração do DbContext
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("FIAPCloudGamesJogos"));

        // Configuração do Elasticsearch
        // var elasticsearchUrl = configuration["Elasticsearch:Url"] ?? "http://localhost:9200";
        // var connectionSettings = new ConnectionSettings(new Uri(elasticsearchUrl))
        //     .DefaultIndex("games")
        //     .EnableDebugMode()
        //     .PrettyJson()
        //     .RequestTimeout(TimeSpan.FromMinutes(2));

        //var client = new ElasticClient(connectionSettings);
        //services.AddSingleton<IElasticClient>(client);

        // Configuração do JWT
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            var jwtKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key não está configurado");
            var issuersKeys = configuration.GetSection("Jwt:IssuersKeys").GetChildren()
                .ToDictionary(x => x.Key, x => x.Value ?? "");

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true, // Valida se é para FIAP.CloudGames.Client
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuers =
                [
                    configuration["Jwt:Issuer"],
                    "FIAP.CloudGames.Usuario.API",
                    "FIAP.CloudGames.Pagamento.API"
                ],
                ValidAudience = configuration["Jwt:Audience"], // FIAP.CloudGames.Client

                // Resolver dinâmico para buscar a chave correta por issuer
                IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
                {
                    var issuer = securityToken?.Issuer;
                    
                    if (string.IsNullOrEmpty(issuer) || !issuersKeys.TryGetValue(issuer, out var key))
                    {
                        // Fallback para a chave padrão
                        return [new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))];
                    }

                    return [new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))];
                }
            };
        });

        // Configuração da Autorização
        services.AddAuthorization();

        // Registro dos serviços
        // services.AddScoped<IElasticsearchService, ElasticsearchService>();
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
