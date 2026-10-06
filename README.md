# Otherworld: servidor dos amigos

Instalador e atualizador do modpack **Otherworld [Dungeons & Dragons] v8 HF2** (Minecraft 1.20.1, Forge 47.4.20) para quem joga pelo TLauncher.

## Como instalar

1. Baixe o `InstaladorOtherworld.exe` na página de [Releases](../../releases/latest).
2. Feche o Minecraft e o TLauncher.
3. Abra o instalador e clique em **Instalar / Atualizar**.
4. No TLauncher, escolha **Forge 1.20.1** e coloque **8 GB** de memória.
5. Entre no jogo. O servidor já está na lista do Multijogador.

Na primeira vez o Windows pode mostrar "O Windows protegeu o computador", porque o programa não é assinado. Clique em **Mais informações → Executar assim mesmo**.

Para atualizar o pack depois, é só abrir o mesmo instalador de novo. Ele baixa só o que mudou.

## O que o instalador faz

- Guarda uma cópia das suas pastas `mods`, `config`, `defaultconfigs`, do `options.txt` e do `servers.dat` em `.minecraft\backup` antes de mexer em qualquer coisa.
- Baixa os mods, resource packs e shaders **direto do CurseForge** e confere o hash de cada arquivo.
- Instala as configurações do pack, a tradução PT-BR e as teclas sem conflito.
- Coloca o servidor na lista do Multijogador, sem apagar os servidores que você já tinha.
- Só mexe na pasta `.minecraft` do seu usuário e não pede administrador.
- Grava um log em `.minecraft\instalador-otherworld-log.txt`.

## Código

- `src/`: código do instalador (C#, .NET Framework 4.8, que já vem no Windows 10 e 11).
- `gerar_pacote.py`: gera os arquivos de cada versão publicada no Releases.

Compilar:

```
csc /target:winexe /out:InstaladorOtherworld.exe /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll src\Instalador.cs src\Nbt.cs
```

O modpack Otherworld é de Luna Pixel Studios. Os mods pertencem aos seus autores e são baixados do CurseForge.
