# TF2 Launcher Multi-Store Pricing Handoff (Claude DOCX Consolidado)

## 1. Contexto do projeto
- Projeto: Project Eternal Launcher
- Stack: WPF + .NET 8
- Área: aba Inventory
- Objetivo: sistema de preços multi-loja com robustez de produção

Arquivos do launcher que entram no escopo:
- src/LauncherTF2/ViewModels/InventoryViewModel.cs
- src/LauncherTF2/Services/InventoryPricingService.cs
- src/LauncherTF2/Views/InventoryView.xaml

Novo projeto recomendado:
- PricingAggregator (ASP.NET Core)

## 2. Problema real
A chamada direta do launcher desktop para APIs dos marketplaces está sendo bloqueada por WAF/anti-bot (Cloudflare e similares). Resultado prático: indisponibilidade frequente e inconsistência de preços por loja.

## 3. Decisão arquitetural
Implementar um backend agregador de preços. O launcher passa a chamar somente esse backend.

Motivos:
- Não expõe chaves de API no cliente desktop
- Centraliza retry, timeout, cache e observabilidade
- Reduz impacto dos bloqueios anti-bot no cliente
- Permite adicionar/remover fontes sem obrigar release do launcher

## 4. Modelo de dados alvo
StorePrice (um por loja)
- StoreName
- Status: Live, Approx ou Unavailable
- PriceUsd (quando aplicável)
- PriceKeys (quando aplicável)
- ListingUrl (botão Open)
- Source: api, fallback ou timeout
- UpdatedAt

ItemPriceResult (resposta completa)
- ItemName
- Sku (opcional)
- Prices (lista de StorePrice)
- ResolvedAt

## 5. Backend PricingAggregator

### 5.1 Estrutura sugerida
- PricingAggregator/Controllers/PricesController.cs
- PricingAggregator/Services/IPricingSource.cs
- PricingAggregator/Services/PricingAggregatorService.cs
- PricingAggregator/Sources/PricesTfSource.cs
- PricingAggregator/Sources/SteamMarketSource.cs
- PricingAggregator/Sources/BackpackTfSource.cs
- PricingAggregator/Models/ItemPriceResult.cs
- PricingAggregator/appsettings.json

### 5.2 Contrato de fonte
Cada fonte implementa:
- StoreName
- GetPriceAsync(itemName, sku, cancellationToken)

### 5.3 Fontes propostas
PricesTfSource
- Primária
- Endpoint alvo: api2.prices.tf por SKU
- Sem chave no cenário base

SteamMarketSource
- Fallback universal
- Endpoint alvo: Steam market priceoverview
- Deve marcar status como Approx

BackpackTfSource
- Opcional para chaves/ref
- Pode exigir API key

### 5.4 Agregador (fan-out + cache)
- Chamar todas as fontes em paralelo
- Timeout por fonte: 5 segundos
- Cache por item (sku ou nome): 15 minutos
- Em falha de fonte: retornar Unavailable sem quebrar a resposta global

### 5.5 Endpoint HTTP
GET /api/prices?item=Scrap%20Metal&sku=5000;6

Regra:
- item obrigatório
- sku opcional
- resposta sempre normalizada em ItemPriceResult

### 5.6 DI e infraestrutura
- AddMemoryCache
- HttpClient por fonte
- Registro de todas as fontes via interface
- Registro do serviço agregador

## 6. Launcher client
No launcher, o InventoryPricingService deve virar cliente fino do backend agregador.

Responsabilidades no cliente:
- Montar URL com item e sku
- Fazer chamada HTTP
- Tratar null/falha de forma amigável

Responsabilidades removidas do cliente:
- Retry por loja
- Timeout complexo por fonte
- Parsing de múltiplos formatos de marketplace
- Fallback entre fontes

## 7. ViewModel e UI
No InventoryViewModel:
- Ao selecionar item, carregar preços de forma assíncrona
- Nunca bloquear thread de UI
- Controlar IsLoadingPrices
- Limpar e popular coleção StorePrices

Na View:
- Spinner enquanto IsLoadingPrices for true
- Card por loja com:
  - nome da loja
  - status
  - preço em USD e/ou keys/ref
  - botão Open com ListingUrl

## 8. Plano incremental de rollout
Fase 1
- Subir PricingAggregator com PricesTfSource + SteamMarketSource

Fase 2
- Integrar launcher com backend e remover lógica antiga de scraping direto

Fase 3
- Ajustar UX de estados (Live, Approx, Unavailable)

Fase 4
- Adicionar novas fontes estáveis gradualmente

## 9. Segurança e compliance
- Nunca colocar API keys no launcher nem no repositório
- Chaves somente no host backend via variável de ambiente
- URL do agregador deve vir de config no launcher
- Respeitar ToS/robots das fontes
- Priorizar APIs JSON documentadas em vez de scraping HTML

## 10. Critérios de aceitação
1. UI nunca trava ao carregar preços
2. Sem erro técnico cru para usuário final
3. Sempre que não houver preço, mostrar estado amigável e manter botão Open
4. Itens comuns devem ter preço consistente em pelo menos uma fonte estável
5. Fácil expansão para novas lojas sem release obrigatório do launcher

## 11. Prompts sugeridos para implementação assistida
PricesTfSource
- Implementar GetPriceAsync com HttpClient e parse do SellPrice em centavos, retornando Live em sucesso e Unavailable em falha

SteamMarketSource
- Implementar GetPriceAsync no endpoint priceoverview e parse de lowest_price para decimal, retornando Approx

PricingAggregatorService
- Implementar Task.WhenAll com timeout de 5s por fonte e cache de 15 minutos por chave sku ou item

InventoryViewModel
- Implementar LoadPricesForItem assíncrono, com IsLoadingPrices, limpeza/população de StorePrices e sem bloquear UI

---

Origem deste handoff:
- Consolidado a partir do arquivo DOCX de referência enviado no repositório.
