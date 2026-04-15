# Integração do XenosNative.dll ao Launcher (background)

Este guia descreve como integrar a DLL nativa compilada (`XenosNative.dll`) ao seu launcher C# para que o launcher injete uma DLL (por exemplo `overlay.dll`) no processo do TF2 quando o jogo estiver no menu.

## Resumo rápido
- Compile `XenosNative.dll` (x64 Release) a partir de `src/XenosNative.vcxproj`.
- Coloque `XenosNative.dll` junto ao executável do launcher (ou configure o projeto para copiá-la para a saída).
- No launcher C#, chame a função exportada `Xenos_InjectByPid` via P/Invoke assim que detectar que o TF2 alcançou o menu.

## Pré-requisitos
- `XenosNative.dll` compilado para x64 (Release).
- Launcher (processo .NET) executando como x64 (PlatformTarget = x64).
- A DLL alvo (ex.: `overlay.dll`) deve ser x64.

## Build do wrapper (exemplo)
Abra um Developer Command Prompt (x64) e rode:

```powershell
cd "c:\Users\mathe\Downloads\Xenos-master\src"
msbuild XenosNative.vcxproj /p:Configuration=Release /p:Platform=x64
```

Depois copie `Release\x64\XenosNative.dll` para a pasta do launcher (ou configure o projeto para copiar automaticamente).

## Como incluir a DLL no projeto C# (opções)
1. Coloque `XenosNative.dll` numa pasta do projeto do launcher (ex.: `native\XenosNative.dll`) e marque o arquivo em propriedades como `Copy to Output Directory = PreserveNewest`.

Exemplo de trecho no `.csproj`:

```xml
<ItemGroup>
  <None Include="native\XenosNative.dll">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

2. Ou use um Target para copiar do diretório de build do wrapper para a saída do launcher:

```xml
<Target Name="CopyNative" AfterTargets="Build">
  <Copy SourceFiles="$(SolutionDir)src\Release\x64\XenosNative.dll" DestinationFolder="$(OutputPath)" SkipUnchangedFiles="true" />
</Target>
```

## P/Invoke — declaração segura e wrapper async (exemplo)
Adicione o seguinte helper no projeto do launcher (por ex. `NativeInjector.cs`):

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

internal static class NativeInjector
{
    [DllImport("XenosNative.dll", EntryPoint = "Xenos_InjectByPid", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Xenos_InjectByPid(uint pid, string dllPath);

    public static Task<int> InjectAsync(Process target, string dllPath)
    {
        if (!Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("Launcher must run as a 64-bit process to inject into x64 TF2.");

        if (target == null || target.HasExited)
            throw new ArgumentException("Target process invalid or exited.");

        if (!File.Exists(dllPath))
            throw new FileNotFoundException("DLL not found", dllPath);

        // Run P/Invoke off the UI thread so we don't block
        return Task.Run(() =>
        {
            try
            {
                int res = Xenos_InjectByPid((uint)target.Id, dllPath);
                return res;
            }
            catch (DllNotFoundException)
            {
                // Native wrapper missing
                return -1000;
            }
            catch (Exception)
            {
                return -1001;
            }
        });
    }
}
```

## Exemplo de uso no fluxo do launcher
No ponto onde o launcher detecta que o TF2 chegou ao menu (código que você já tem — `proc.MainWindowHandle` + espera extra), chame:

```csharp
string dllPath = Path.Combine(AppContext.BaseDirectory, "overlay.dll");
int res = await NativeInjector.InjectAsync(proc, dllPath);
if (res == 0)
    Log("Injection succeeded");
else
    Log($"Injection failed: 0x{res:X}");
```

Se preferir bloquear com timeout:

```csharp
var task = NativeInjector.InjectAsync(proc, dllPath);
if (await Task.WhenAny(task, Task.Delay(15000)) == task)
{
    int res = task.Result;
    // checar res
}
else
{
    // timeout
}
```

## Mapeamento de códigos de retorno (conforme wrapper atual)
- `0` — sucesso (LoadLibraryW retornou handle não-nulo no processo remoto).
- `-1` — argumentos inválidos (pid ou caminho nulo).
- `-2` — `OpenProcess` falhou (PID incorreto ou acesso negado).
- `-3` — `VirtualAllocEx` falhou.
- `-4` — `WriteProcessMemory` falhou.
- `-5` — `GetModuleHandleW("kernel32.dll")` falhou (muito improvável).
- `-6` — `GetProcAddress(LoadLibraryW)` falhou.
- `-7` — `CreateRemoteThread` falhou.
- `-8` — thread remota retornou `0` (LoadLibraryW falhou na target).
- `-1000` e `-1001` — códigos retornados pelo wrapper C# para indicar DLL ausente ou exceção.

> Observação: se quiser que o wrapper retorne o handle do módulo remoto (valor do `GetExitCodeThread`) em vez de `0` para sucesso, eu posso ajustar o wrapper nativo para retornar esse valor.

## Permissões e problemas comuns
- Se `OpenProcess` falhar com `AccessDenied`, tente executar o launcher como Administrador.
- AV/EDR pode bloquear `WriteProcessMemory/CreateRemoteThread` — adicione exceção ou teste com AV desativado.
- Verifique bitness: launcher (x64) e `overlay.dll` devem ser ambos x64.

## Verificando se a DLL foi carregada
- Use Process Explorer (Sysinternals) — verifique a lista de módulos do processo TF2.
- Em C# você pode tentar ler `proc.Modules` (pode necessitar de privilégios):

```csharp
bool loaded = false;
try
{
    foreach (ProcessModule m in proc.Modules)
        if (string.Equals(Path.GetFileName(m.FileName), "overlay.dll", StringComparison.OrdinalIgnoreCase)) { loaded = true; break; }
}
catch { /* permissão negada possível */ }
```

## Fallbacks e recomendações
- Se a injeção falhar frequentemente por proteção do processo, podemos integrar BlackBone/manual-map no wrapper nativo e compilar o wrapper contra a lib BlackBone (mais complexo, requer dependências e teste). Diga se quer essa opção.
- Logue retornos e erros para `launcher.log` para facilitar debugging.

## Checklist para integração automática pela IA
- [ ] Compilar `XenosNative.dll` x64 (Release).
- [ ] Mover/copiar `XenosNative.dll` para a pasta do launcher (ou incluir no projeto com `CopyToOutputDirectory`).
- [ ] Adicionar `NativeInjector.cs` (ou incorporar a função) ao projeto do launcher.
- [ ] Garantir `overlay.dll` (x64) presente no output do launcher.
- [ ] Atualizar `csproj` do launcher com `PlatformTarget>x64</PlatformTarget>`.
- [ ] Testar: iniciar TF2 via launcher e confirmar `Injection succeeded` no log.

---

Se quiser, eu mesmo faço essas modificações de código no seu projeto: adiciono `NativeInjector.cs` ao launcher, altero o `.csproj` para copiar `XenosNative.dll` e insiro a chamada automática no fluxo que detecta o menu do TF2. Diga se quer que eu aplique as alterações automaticamente e confirme o caminho final onde a DLL alvo (`overlay.dll`) ficará localizada (por padrão vou assumir a pasta do launcher).