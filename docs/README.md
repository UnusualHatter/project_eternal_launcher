# Documentação

## Escopo Atual (MVP x Mock)

### MVP
- **App desktop WPF em .NET 8** com estrutura MVVM e navegação principal.
- **Configurações persistidas** em `settings.json` com validações (`SettingsService`).
- **Launch do TF2** via protocolo `steam://rungameid/440//...` (`GameService`).
- **Integração com tray** usando `Hardcodet.NotifyIcon.Wpf` (`MainWindow`).
- **Gestão básica de mods locais** (scan/toggle/estado) em `ModManagerService`.
- **Discord RPC base** com start/stop e atualização por leitura de log (`Tf2RichPresenceService`).

### Mock / Parcial
- **Metadados de mods** ainda usam fallback simples quando não há arquivo de info.
- **Fila/matchmaking no RPC** está parcialmente implementado (parser incompleto).
- **Inventário** é exibido por página externa (`backpack.tf`), sem backend próprio no launcher.
- **Blog** é carregado de URL externa, sem persistência offline.

## Critérios práticos de “funcionando”
- Compila com sucesso em `src/LauncherTF2/LauncherTF2.csproj`.
- Fluxo principal abre UI, permite navegar módulos e acionar launch.
- Serviços críticos registram eventos no logger para suporte e troubleshooting.

## Referência rápida
- Documento técnico detalhado: `agents.md`
