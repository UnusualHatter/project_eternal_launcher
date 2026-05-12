# Settings Tab Architecture

Estrutura completa da aba de Configurações: componentes, fluxo de dados, e como cada peça se encaixa.

## 1. Entrada e Inicialização

### Entry Points

- **[SettingsView.xaml](../src/LauncherTF2/Views/SettingsView.xaml)** — UI declarativa (XAML + DataTemplates)
- **[SettingsView.xaml.cs](../src/LauncherTF2/Views/SettingsView.xaml.cs)** — code-behind (scroll sync, preset clicks, event forwarding)
- **[SettingsViewModel.cs](../src/LauncherTF2/ViewModels/SettingsViewModel.cs)** — VM controller (state + commands)
- **[SettingsModel.cs](../src/LauncherTF2/Models/SettingsModel.cs)** — data model (properties + notification)

### Fluxo de Inicialização

1. `MainViewModel` cria a SettingsView + injeta SettingsViewModel como DataContext
2. SettingsView code-behind conecta handlers (`ScrollChanged`, `KeyDown`, `PreviewMouseDown`)
3. SettingsViewModel ctor:
   - Carrega `settings.json` via `SettingsService`
   - Carrega `launcher_config.json`
   - Parseia `autoexec.cfg` existente via `AutoexecParser` (para manter valores do user)
   - Constrói o schema e sidebar via `BuildSchemaAndSidebar()`
   - Conecta listeners a `CurrentSettings.PropertyChanged`

## 2. Sistema de Schema (Declarativo)

### Como Funciona

Em vez de declarar cada setting manualmente em XAML, uma **árvore de objetos** (`SettingCategory → SettingItem`) descreve toda a superfície TF2 em código. XAML é data-templated — um único `ItemsControl` renderiza todas as categorias.

### Estrutura

**[SettingsSchema.cs](../src/LauncherTF2/Services/SettingsSchema.cs)**

```
SettingCategory (id, title)
  └─ SettingItem[] (toggle / slider / choice / preset)
       ├─ Cvar: string (ex: "mat_disable_bloom")
       ├─ CustomEmitter: Func<string[]>? (ex: para inversões)
       ├─ NotCasualCompatible: bool
       ├─ DependsOn + IsEnabledPredicate: gate child rows
```

Exemplo:

```csharp
new SettingCategory("gameplay", "Gameplay")
{
    new ToggleSetting(
        id: "bloom",
        title: "Bloom",
        getter: s => !s.Bloom,  // inverte
        setter: (s, v) => s.Bloom = !v,
        cvar: "mat_disable_bloom",
        customEmitter: s => s.Bloom ? [] : ["mat_disable_bloom 1"]
    ),
    new SliderSetting(...)
}
```

### Data Binding

Cada `SettingItem` wraps uma property de `SettingsModel` via getter/setter delegados:

```csharp
ToggleSetting(getter: s => s.MyProperty, setter: (s, v) => s.MyProperty = v)
```

Quando o usuário clica um toggle → setter → `SettingsModel.PropertyChanged` → todos os `SettingItem` wrappers observam + a UI se atualiza sem rebind completo (eficiente).

### Por Que Assim?

1. **Uma única fonte de verdade** — schema, UI, autoexec, validation tudo em um arquivo
2. **Adicionar setting = 3 linhas** — property em SettingsModel + entrada no schema (mais `CustomEmitter` se inverter cvar)
3. **Migrations automáticas** — `settings.json` velhos que faltam uma property defaultam via property defaults, sem quebra

## 3. Sidebar + ScrollAnchor

### Sidebar Anatomy

Duas partes:

1. **Esquerda** — ScrollViewer com sidebar entries (lista navegável, highlight)
2. **Direita** — ScrollViewer com conteúdo (ContentScroller)

### ScrollAnchor System

[ScrollAnchor.cs](../src/LauncherTF2/Core/ScrollAnchor.cs) — attached property que registra elementos sob nome em um registry per-host:

```csharp
<StackPanel core:ScrollAnchor.Name="general" core:ScrollAnchor.Host="{Binding ElementName=ContentScroller}">
```

No Loaded, a StackPanel se registra sob o nome "general" no registry do ContentScroller. Possibilita acessar anchors gerados por DataTemplate (onde `x:Name` não funciona).

### Fluxo de Clique Sidebar → Scroll

1. User clica "Gameplay" na sidebar
2. XAML binding dispara `NavigateCategoryCommand`
3. SettingsViewModel: `RequestScroll("gameplay")`
4. `ScrollToCategoryRequested` event dispara com o ID
5. SettingsView code-behind: `OnScrollRequested(sender, "gameplay")`
6. Resolve anchor: `ScrollAnchor.Find(ContentScroller, "gameplay")` → FrameworkElement
7. Dispara animação: `AnimatedScrollHelper.ScrollToElement(ContentScroller, target)`

### Active Highlight

Enquanto usuário scroll a mão (ou via mouse wheel):

- `ContentScroller_ScrollChanged` dispara
- Se NOT animando (guard via `AnimatedScrollHelper.IsAnimating()`):
  - Itera todos os anchors registrados
  - Encontra o que está mais próximo do topo (com `viewportLead = 80px`)
  - Atualiza `ActiveSidebarId`
  - Sidebar bindings atualizam o highlight via `DataTrigger IsActive`

**Se animando:** short-circuit para evitar piscar (o highlight já foi pre-setado no clique).

## 4. Animação de Scroll

### [AnimatedScrollHelper.cs](../src/LauncherTF2/Core/AnimatedScrollHelper.cs) (Reescrito)

Smooth scroll-to-element via `CompositionTarget.Rendering` — o hook mais confiável do WPF, sincronizado com render frame.

**API pública:**

- `ScrollToElement(scroller, target, durationMs, onCompleted)` — iniciar animação
- `IsAnimating(scroller)` — True enquanto mid-flight ou drenando
- `StopAnimation()` — cancel all

**Internals:**

1. Resolve posição do target: `target.TransformToVisual(content).Y`
2. Clamp ao scrollable: `Math.Max(0, Math.Min(position.Y, scrollable.Height))`
3. **Hook `CompositionTarget.Rendering`** se não já hooked
4. Por frame:
   - `t = elapsed / duration` (clamped 0..1)
   - `eased = 1 - (1-t)^3` (ease-out cubic)
   - `offset = from + (to - from) * eased`
   - `scroller.ScrollToVerticalOffset(offset)`
5. Ao atingir t=1.0:
   - Pin final: `ScrollToVerticalOffset(to)`
   - Entra em drain window (2 frames extras com IsAnimating=true)
   - Dispara `onCompleted` callback
6. Quando idle, **desconecta** CompositionTarget.Rendering

**Por que CompositionTarget.Rendering?**

- Sincronizado com render loop WPF (~60fps)
- Não compete com dispatcher (que tem layout, events, etc)
- `Stopwatch` para tempo real (frames perdidos ≠ atraso)
- Mais robusto que DispatcherTimer ou PropertyChangedCallback-per-frame

**Drain Window:**

Após animação terminar, `IsAnimating()` ainda retorna true por 2 ticks de dispatcher. WPF posta alguns ScrollChanged atrasados após o último `ScrollToVerticalOffset` — sem o drain, `ContentScroller_ScrollChanged` recomputaria anchor com posição final mas medidas antigas, pisca o highlight brevemente.

## 5. Autoexec: Geração e Parsing

### Write Path: SettingsModel → autoexec.cfg

[AutoexecWriter.cs](../src/LauncherTF2/Services/AutoexecWriter.cs)

1. Iterá schema completo
2. Por item: chama `EmitCvarLines()` (retorna `["cvar value"]` ou `[]` se off/default)
3. Agrupa por categoria header (`// --- Gameplay ---`)
4. Escreve entre marcadores:
   ```
   // === ETERNAL LAUNCHER MANAGED BLOCK ===
   // --- Gameplay ---
   mat_disable_bloom 1
   ...
   // === END ETERNAL LAUNCHER MANAGED BLOCK ===
   ```
5. Preserva tudo fora dos marcadores (user-owned content)

**CustomEmitter:**

Alguns cvars não seguem `cvar value` — ex: `mat_disable_bloom` é invertido (Property=true → escreve `mat_disable_bloom 1`, Property=false → nada). CustomEmitter permite formatter customizado:

```csharp
new ToggleSetting(..., customEmitter: s => s.Bloom ? [] : ["mat_disable_bloom 1"])
```

### Read Path: autoexec.cfg → SettingsModel

[AutoexecParser.cs](../src/LauncherTF2/Services/AutoexecParser.cs)

Roda no startup. Parse o arquivo **inteiro** (managed + user):

1. Itera cada linha
2. Se `cvar value` → lookup no schema, aplica setter
3. Se dentro de managed block → apenas valores do launcher
4. Fora do managed block → ignora (user-owned)
5. Espelha cada `CustomEmitter` do schema (ex: inverte `mat_disable_bloom` de volta)

**Por que útil?** User com autoexec hand-coded instala o launcher → valores levantam automaticamente na UI no primeiro run, sem perder personalizações.

## 6. Persistence

### SettingsService

[SettingsService.cs](../src/LauncherTF2/Services/SettingsService.cs)

Responsável por carregar/salvar:

- **`settings.json`** — TF2 cvars + launch args (por SettingsModel)
- **`launcher_config.json`** — prefs do launcher (UI, logging, tray)

### Fluxo

1. Property change em SettingsModel → `PropertyChanged`
2. SettingsViewModel listener → `SaveSettings(currentSettings)`
3. SettingsService:
   - Serializa model → JSON
   - Escreve `settings.json`
   - Chama `AutoexecWriter.WriteToAutoexec()` (gera cfg)

Salva **immediately** (não é buffered), garante que um crash ou força-fechar perde no máximo a change em progress.

## 7. Presets

### PerformancePresets + NetworkPresets

[SettingsPresets.cs](../src/LauncherTF2/Services/SettingsPresets.cs)

Funções que bulk-apply múltiplos cvars de uma vez:

```csharp
PerformancePresets.ApplyMaxFps(settings);  // desliga tudo pra FPS
NetworkPresets.ApplyCompetitive(settings);  // tunas cl_interp/rate
```

Cada preset **só toca suas próprias cvars** — não toca em blocos alheios. Quando a preset roda → model PropertyChanged notifica cada wrapper → UI atualiza apenas as rows afetadas (eficiente, sem rebind).

### UI Integration

DataTemplate de PresetSetting renderiza chips (botões) para cada preset. Clique → `PresetButton_Click` code-behind → `FindPresetSetting()` walks logical/visual tree (DataTemplate pode desconectar logical scope) → `preset.ApplyById(id)`.

## 8. Sidebar Entries: Fixa + Schema

Sidebar é união de:

1. **Fixed entries:**
   - "General" (TF2 path, display, launch behavior)
   - Categorias do schema (Gameplay, Competitive, Performance, etc)
   - "Launcher" (debug log, tray, notifications)
   - "Personalization" (theme picker)
   - "Binds" (custom key binds)

Tudo num `ObservableCollection<SidebarEntry>` que inclui id + title. O highlight via `ActiveSidebarId` → `DataTrigger` na sidebar.

## 9. Validação

### TF2 Path

`ValidateTf2Path()` checa se `{path}/cfg` existe. Valida logo ao desserializar settings e quando user muda a path (binding `UpdateSourceTrigger=PropertyChanged` + PropertyChanged handler).

Mostra chip verde ✓ ou vermelho ⚠ com mensagem.

### Launch Args

`SyncLaunchOptions()` reconstrói o campo automaticamente a partir das checkboxes/sliders. User pode editar manualmente, mas custom flags que não foram tocadas pelo launcher são preservadas.

Flags obsoletas (`-no_steam_controller`, etc) são listradas quando resynced.

## 10. Keybinds (Bonus Section)

Aba "Binds" permite custom TF2 key binds:

- UI: lista (remove), input field (name), comando (say hello), key display
- User clica um bind → `StartListeningCommand` ativa "listening mode"
- Next key press (mouse button ou teclado) → captured, apply ao bind
- Escape cancela
- Saved em `SettingsModel.Binds` ObservableCollection → auto-persist

Mapping: `Key.A` → `"a"`, `Key.Mouse1` → `"mouse1"`, etc. ([SettingsViewModel.cs:497-583](../src/LauncherTF2/ViewModels/SettingsViewModel.cs#L497-L583))

## 11. File Contracts

**Em disco:**

- `settings.json` — SettingsModel serializado
- `launcher_config.json` — LauncherConfig (preferências do launcher)
- `tf/cfg/autoexec.cfg` — gerado por AutoexecWriter (managed + user-owned blocks)

**Em memória:**

- `SettingsModel` — instance única, carregada no startup, observa PropertyChanged
- `SettingsViewModel` — instance única por tab, mantém schema + sidebar state
- Schema — built once, nunca reconstruído (wrappers observam model, não schema)

## 12. Troubleshooting Checklist

- **Clique na sidebar não scrolla?** → `OnScrollRequested` é chamado? `ScrollAnchor.Find` acha o alvo? Logging em `AnimatedScrollHelper.OnRender`.
- **Highlight pisca?** → `IsAnimating()` gate não funciona? `ContentScroller_ScrollChanged` está sendo chamado durante anim?
- **Setting novo não persiste?** → Adicionado property em SettingsModel? Adicionado ao schema? `PropertyChanged` listener conectado?
- **Autoexec não escreve?** → `AutoexecWriter.WriteToAutoexec()` chamado após save? Arquivo locked?
- **Settings.json velhos não carregam?** → Property missing/null? Default value em SettingsModel ctor deveria rescatar.
- **Preset não funciona?** → Preset func chama `model.PropertyChanged`? UI bound aos getters certos?

## 13. Sequence Diagram: Clique Sidebar até Highlight Atualizar

```
User                    UI                 ViewModel            Helper              Services
 │                      │                     │                  │                   │
 ├─ click "Gameplay" ──>│                     │                  │                   │
 │                      │─ Navigate Cmd ─────>│                  │                   │
 │                      │                     │─ RequestScroll ──┐                   │
 │                      │                     │  ("gameplay")    │                   │
 │                      │ ScrollToCategoryRequested ───────────┐│                   │
 │                      │<─────────────────────────────────────┘│                   │
 │                      │ OnScrollRequested()  │                │                   │
 │                      │ ├─ Find anchor      │                │                   │
 │                      │ └─ ScrollToElement  ──────────────────>│                   │
 │                      │                     │                │ UpdateLayout()    │
 │                      │                     │                ├─>    Services      │
 │                      │                     │                │ Compute to       │
 │                      │                     │                ├──────────────────>│
 │                      │                     │                │ Hook Rendering   │
 │                      │                     │                │ per frame: t++    │
 │                      │                     │                ├─ offset = ease   │
 │                      │  ScrollChanged ◄────────────────────┤ ScrollToVertical  │
 │                      │ (each frame)        │                │ (per frame)       │
 │                      │ if !IsAnimating():  │                │                   │
 │                      │   ActiveSidebarId ──>│ SyncActive     │                   │
 │                      │                     │                │                   │
 │                      │◄────────────────────────────────────┤                   │
 │                      │ (2-frame drain)     │                │                   │
 │◄── highlight updates │                     │                │                   │
```

## 14. Referências Internas

- **[SettingsView.xaml.cs](../src/LauncherTF2/Views/SettingsView.xaml.cs)** — scroll/preset/key handlers
- **[SettingsViewModel.cs](../src/LauncherTF2/ViewModels/SettingsViewModel.cs)** — logic (preset, keybind, reset, sync)
- **[SettingsModel.cs](../src/LauncherTF2/Models/SettingsModel.cs)** — data + PropertyChanged
- **[SettingsSchema.cs](../src/LauncherTF2/Services/SettingsSchema.cs)** — declarative settings tree
- **[AutoexecWriter.cs](../src/LauncherTF2/Services/AutoexecWriter.cs)** — serialize to cfg
- **[AutoexecParser.cs](../src/LauncherTF2/Services/AutoexecParser.cs)** — parse from cfg
- **[SettingsService.cs](../src/LauncherTF2/Services/SettingsService.cs)** — persistence (JSON)
- **[AnimatedScrollHelper.cs](../src/LauncherTF2/Core/AnimatedScrollHelper.cs)** — smooth scroll-to-element
- **[ScrollAnchor.cs](../src/LauncherTF2/Core/ScrollAnchor.cs)** — register/resolve elements by name

## 15. Convenções Importantes

- **Sempre use ServiceLocator** para acessar SettingsService, não instanciar
- **Logging:** `Logger.LogInfo("[Settings] message")` — bracket do módulo
- **Async:** naming com `Async` suffix
- **Presets:** cada preset muta só seus cvars, model PropertyChanged notifica o resto
- **Schema:** adicionar setting = prop + entry no schema, pronto
- **Custom Emitter:** para cvars com semântica invertida ou multi-line
- **DependsOn:** para aninhar toggles (child só habilitado se parent ativo)

