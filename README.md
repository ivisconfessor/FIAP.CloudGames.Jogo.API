# FIAP Cloud Games - Microsserviço de Jogos

Microsserviço responsável pelo gerenciamento de jogos da plataforma FIAP Cloud Games, com busca avançada usando Elasticsearch.

## 🚀 Funcionalidades

- **CRUD de Jogos**: Criação, leitura, atualização de jogos
- **Busca Avançada**: Busca de jogos usando Elasticsearch com filtros por gênero, plataforma e preço
- **Recomendações**: Sistema de recomendação de jogos baseado em popularidade
- **Analytics**: Agregações para métricas como jogos por gênero e plataforma
- **Event Sourcing**: Registro de todos os eventos relacionados a jogos
- **Observabilidade**: Logs estruturados e rastreamento distribuído

## 🏗️ Arquitetura

Este microsserviço segue os princípios de:

- **Domain-Driven Design (DDD)**
- **Clean Architecture**
- **Event Sourcing** para auditoria completa
- **CQRS Pattern** com Elasticsearch para queries otimizadas
- **Observabilidade** com traces distribuídos

## 📋 Endpoints

### Protegidos (requer autenticação)

#### CRUD
- `POST /api/games` - Criar novo jogo (Admin)
- `GET /api/games` - Listar todos os jogos
- `GET /api/games/{id}` - Obter jogo por ID
- `PUT /api/games/{id}` - Atualizar jogo (Admin)

#### Busca e Recomendações
- `POST /api/games/search` - Busca avançada de jogos
- `GET /api/games/popular?count={count}` - Obter jogos populares
- `GET /api/games/recommendations` - Obter recomendações personalizadas

#### Analytics
- `GET /api/games/analytics/by-genre` - Agregação de jogos por gênero
- `GET /api/games/analytics/by-platform` - Agregação de jogos por plataforma

### Públicos
- `GET /api/health` - Health check do serviço
- `GET /api/events/{aggregateId}` - Obter eventos do jogo (Autenticado)

## 🔧 Tecnologias Utilizadas

- **.NET 8.0**
- **Entity Framework Core** (In-Memory Database)
- **Elasticsearch** (NEST 7.17.5) para busca avançada
- **JWT Bearer Authentication**
- **FluentValidation** para validação de entrada
- **Serilog** para logging estruturado
- **OpenTelemetry** para observabilidade
- **Swagger/OpenAPI** para documentação

## 🏃 Como Executar

### Pré-requisitos

- .NET 8.0 SDK
- Elasticsearch 7.17+ (opcional para testes)

### Executar localmente

```bash
cd src
dotnet restore
dotnet run
```

A API estará disponível em:
- HTTP: http://localhost:5002
- HTTPS: https://localhost:7002
- Swagger: http://localhost:5002/swagger

### Executar com Docker

```bash
docker build -t fiap-cloudgames-jogo-api .
docker run -p 5002:80 fiap-cloudgames-jogo-api
```

### Executar Elasticsearch com Docker

```bash
docker run -d \
  --name elasticsearch \
  -p 9200:9200 \
  -e "discovery.type=single-node" \
  -e "xpack.security.enabled=false" \
  docker.elastic.co/elasticsearch/elasticsearch:7.17.5
```

## 🔍 Elasticsearch

### Indexação Automática

Todos os jogos são automaticamente indexados no Elasticsearch quando:
- Um jogo é criado
- Um jogo é atualizado

### Busca Avançada

A busca suporta:
- **Full-text search** em título e descrição
- **Filtros** por gênero, plataforma e faixa de preço
- **Fuzzy matching** para correção de erros de digitação
- **Paginação** de resultados

Exemplo de busca:

```json
POST /api/games/search
{
  "query": "RPG",
  "genre": "RPG",
  "platform": "PC",
  "minPrice": 0,
  "maxPrice": 200,
  "page": 1,
  "pageSize": 10
}
```

### Agregações

O sistema fornece agregações para:
- Distribuição de jogos por gênero
- Distribuição de jogos por plataforma
- Jogos mais populares (baseado em visualizações e compras)

## 📊 Event Sourcing

Todos os eventos relacionados a jogos são registrados:

- `GameCreatedEvent` - Quando um jogo é criado
- `GameUpdatedEvent` - Quando um jogo é atualizado
- `GameViewedEvent` - Quando um jogo é visualizado
- `GameSearchedEvent` - Quando uma busca é realizada

Os eventos podem ser consultados através do endpoint `/api/events/{aggregateId}`.

## 🔍 Observabilidade

### Logs

Logs estruturados são gerados com Serilog, incluindo:
- Informações de requisição
- Eventos de negócio
- Interações com Elasticsearch
- Erros e exceções

### Traces

OpenTelemetry é utilizado para rastreamento distribuído, permitindo:
- Rastreamento de requisições entre microsserviços
- Análise de performance
- Identificação de gargalos

## 🌐 Integração com outros Microsserviços

Este microsserviço se comunica com:

- **FIAP.CloudGames.Usuario.API** (porta 5001) - Para autenticação e autorização
- **FIAP.CloudGames.Pagamento.API** (porta 5003) - Para processar compras

As URLs são configuráveis através do `appsettings.json`:

```json
"ServiceUrls": {
  "UsuarioAPI": "http://localhost:5001",
  "PagamentoAPI": "http://localhost:5003"
}
```

## 🎮 Dados Iniciais

Para demonstração, alguns jogos são criados automaticamente ao iniciar a aplicação:

- The Witcher 3 (RPG, PC)
- Cyberpunk 2077 (RPG, PC)
- FIFA 24 (Esporte, PlayStation)
- Call of Duty (FPS, Xbox)
- Minecraft (Sandbox, PC)

## 📝 Licença

Este projeto é parte do Tech Challenge da FIAP - Pós-Tech.
