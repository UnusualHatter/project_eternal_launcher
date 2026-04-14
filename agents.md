# agents.md

## Contexto do Projeto
- **Nome**: Project Eternal: TF2 Launcher
- **Tipo**: Aplicativo desktop WPF em **.NET 8**
- **Objetivo**: Launcher avançado para Team Fortress 2 com gerenciamento de configurações, mods, integração com Discord Rich Presence e execução em tray.
- **Plataforma alvo**: Windows 10/11

## Estrutura Principal
- `src/LauncherTF2/Core`: utilitários base (logger, comandos, infraestrutura MVVM)
- `src/LauncherTF2/Models`: modelos de dados
- `src/LauncherTF2/Services`: regras de negócio e integrações (Steam, autoexec, injeção, RPC)
- `src/LauncherTF2/ViewModels`: camada MVVM de estado e comandos
- `src/LauncherTF2/Views`: telas WPF (XAML + code-behind)
- `resources/Assets`: imagens e recursos visuais
- `cfg/autoexec.cfg`: configuração de jogo de referência

## Stack Técnica
- **Framework**: `net8.0-windows`
- **UI**: WPF + MVVM
- **Pacotes relevantes**:
  - `Hardcodet.NotifyIcon.Wpf` (tray)
  - `DiscordRichPresence` (RPC)
  - `Microsoft.Web.WebView2` (conteúdo web embutido)
  - `System.Text.Json` (API parsing)
  - `System.IO.Compression` (extração de mods)
  - `System.Net.Http` (comunicação GameBanana API)

## Fluxo de Execução (alto nível)
1. `App.xaml` inicia o ciclo de vida da aplicação.
2. `MainWindow` sobe interface principal e integra o ícone de tray.
3. `MainViewModel` orquestra navegação entre módulos (Home, Mods, Settings, etc.).
4. Serviços especializados executam persistência, integração com TF2/Steam, RPC e utilidades.

## Correções Aplicadas Nesta Iteração
1. **Erro de compilação em ícone de tray** (`Views/MainWindow.xaml.cs`)
   - Removido uso inválido de `ImageSource.Save`.
   - Implementada resolução do ícone via `Icon.ExtractAssociatedIcon` do executável.
2. **Erro de assinatura de logger** (`Core/Logger.cs`)
   - Adicionada sobrecarga `LogWarning(string message, Exception? exception)`.
3. **Chamadas incompatíveis de logger**
   - Passaram a compilar sem alterações adicionais de call site após sobrecarga.
4. **Warning CS1998** (`Services/InjectionService.cs`)
   - `MonitorAndInject` convertido para método síncrono (`void`) por não usar `await`.
5. **Navegador de Mods Integrado** (v2.0)
   - Implementação completa de navegador de mods com GameBanana API v11.
   - Sistema de categorias dinâmicas (Skins, Maps, UI, Sound, etc.).
   - Funcionalidade de ordenação (new, updated, obsolete, at_selection).
   - Download e instalação automática de mods com extração de arquivos.
   - Interface melhorada com loading indicators e feedback visual.
   - Tratamento robusto de erros de rede e fallback para cache local.

## Comentários de Código Incluídos
- Foram adicionados comentários nos trechos alterados para documentar:
  - motivo da escolha do ícone de tray;
  - intenção da sobrecarga de warning no logger;
  - natureza síncrona da rotina de monitoramento/injeção.

## Build e Verificação
- Build validada com sucesso no caminho correto do projeto:
  - `dotnet build src/LauncherTF2/LauncherTF2.csproj`

## Status Atual: MVP vs Mock

### MVP (implementado e funcional no código)
- **Inicialização e shell do app**: ciclo de vida WPF carregando `MainWindow` e navegação MVVM em `MainViewModel`.
- **Build e execução local**: projeto compila em `net8.0-windows` e scripts de execução estão operacionais no ambiente atual.
- **Persistência de configurações**: leitura/escrita de `settings.json` com validação de faixa em `SettingsService`.
- **Geração/atualização de autoexec**: escrita de configurações para arquivo de jogo via `AutoexecWriter` (quando caminho Steam é válido).
- **Lançamento do TF2**: disparo via `steam://rungameid/440//...` em `GameService` com tratamento de erros e logs.
- **Tray e minimizar**: integração com `Hardcodet.NotifyIcon.Wpf` na `MainWindow`.
- **Gerenciamento de mods (local)**: varredura de `Resources/Mods`, toggle de estado e persistência em `mod_state.json` (`ModManagerService`).
- **RPC Discord (base funcional)**: start/stop do client, monitoramento de `console.log` e atualização de presença em `Tf2RichPresenceService`.
- **Navegador de Mods Online**: integração completa com GameBanana API v11 para busca, download e instalação de mods.
- **Sistema de Categorias**: filtragem dinâmica por seções do GameBanana (Skins, Maps, UI, Sound, etc.).
- **Ordenação e Busca**: múltiplos critérios de ordenação e busca em tempo real.
- **Download Automático**: extração de arquivos ZIP e instalação na pasta custom do TF2.

### Mock / Placeholder (parcial ou sem integração completa)
- **RPC por eventos avançados de matchmaking**: parsing de fila está parcialmente esboçado (`TF_Matchmaking_Queue_Caption` sem parser completo).
- **Inventário**: tela aponta para URL da backpack.tf baseada no SteamID detectado; não há pipeline interno de inventário/preços no launcher.
- **Blog/Notícias**: consumo via URL web embutida, sem backend próprio, cache de conteúdo ou sincronização offline.
- **Injeção DLL**: monitor/injeção está implementado, mas depende de ambiente real (processo `hl2` + `casual_fix.dll`) para validação fim-a-fim.
- **Sistema de Avaliação de Mods**: atualmente sem sistema de rating ou reviews integrado.
- **Mod Profiles**: funcionalidade de perfis de configuração de mods ainda não implementada.

### Gap para evoluir de MVP para Produto
- **Confiabilidade**: adicionar testes automatizados para `SettingsService`, `AutoexecWriter`, `ModManagerService` e `GameBananaModService`.
- **Observabilidade**: definir métricas de falha/sucesso para launch, RPC, injeção e downloads de mods.
- **UX de validação**: feedback visual mais claro quando caminho Steam, escrita de autoexec, injeção ou downloads falharem.
- **RPC avançado**: completar parser de estados de matchmaking e mapear assets de mapa de forma consistente.
- **Mod Management Avançado**: sistema de dependências entre mods, conflitos e compatibilidade.
- **Cloud Sync**: sincronização de configurações e mods entre dispositivos.
- **Mod Analytics**: estatísticas de uso, popularidade e recomendações personalizadas.

## Observações de Manutenção
- Evitar dependência de APIs de imagem WPF para geração de `System.Drawing.Icon`.
- Preferir sobrecargas de logging consistentes entre níveis (`Info`, `Warning`, `Error`) para reduzir erros de assinatura.
- Manter métodos `async` apenas quando houver trabalho assíncrono real.
- API GameBanana requer tratamento cuidadoso de JSON e gerenciamento de timeouts.
- Cache local é essencial para experiência offline e redução de carga no servidor.

## Próximos Passos Recomendados
- Ajustar tasks VS Code para apontar para `src/LauncherTF2/LauncherTF2.csproj`.
- Incluir testes unitários para serviços críticos (`SettingsService`, `AutoexecWriter`, `GameBananaModService`).
- Adicionar validações de caminho de Steam com feedback visual no UI.
- Implementar sistema de notificações para atualizações de mods e eventos do launcher.
- Otimizar performance do catálogo online com lazy loading e virtualização.
