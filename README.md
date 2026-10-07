# Buzzy

Mascote de companhia, original e não verbal, para desktop Windows 10 e 11 (64 bits): um macaquinho em pixel art com o chapéu de palha e o jeito do Luffy, que circula pelas bordas da tela, reage a cliques e, pelo menu, ganha uma emoção dominante e itens de um "tamagotchi adulto" de desenho animado. Local, sem rede e sem IA; uso pessoal.

## Baixar

- **Site:** https://fishpedrol.github.io/site-buzzy/
- **Release mais recente:** [v0.1.3](https://github.com/fishpedrol/Buzzy/releases/tag/v0.1.3) — `Buzzy-0.1.3-win-x64.exe`, um `.exe` único que não precisa do .NET instalado. O SHA-256 de cada arquivo está ao lado dele na release.

Testado em Windows 10 22H2 e Windows 11 sem o .NET instalado. O executável não tem assinatura digital, então o Windows SmartScreen pode avisar na primeira execução.

## Como usar

- **Arrastar:** clique e arraste; solto no ar, ele cai até o chão. Perto da borda de cima, agarra um cipó; perto de uma lateral, gruda na parede.
- **Esconder:** dois cliques nele o escondem na borda da tela; dois cliques de novo o tiram de lá.
- **Menu:** botão direito no Buzzy ou no ícone da bandeja — pausar o movimento, emoção dominante, itens, energia e configurações.
- **Itens:** invoque pelo menu e arraste o item até o Buzzy para ele usar.
- **Tela cheia:** com um jogo ou vídeo em tela cheia, ele sai do caminho e volta depois.

As configurações ficam em `%LOCALAPPDATA%\Buzzy\settings.json`, com a versão anterior em `settings.json.bak`.

## Compilar a partir do código

Requer o SDK do .NET 10 (versão fixada em `global.json`). No PowerShell, na pasta do projeto:

```powershell
dotnet build src\Buzzy.App\Buzzy.App.csproj -c Release
.\src\Buzzy.App\bin\Release\net10.0-windows\Buzzy.exe
```

O build termina com o portão de segurança (`Resumo: APROVADO`), que procura APIs proibidas no código e no manifesto.

- `src/` — o aplicativo (WPF), o núcleo e a parte visual
- `tools/` — portão de segurança, empacotamento e validação dos clipes
- `assets/` — arte e identidade visual
- `spikes/` — protótipos

## Licença

Copyright (c) 2026 fishpedrol. Todos os direitos reservados: o código pode ser lido e o executável das releases oficiais pode ser usado para fins pessoais e não comerciais. Os termos completos estão em [LICENSE](LICENSE); os componentes de terceiros, no `THIRD-PARTY-NOTICES.txt` de cada release.

Projeto pessoal, gratuito e não oficial, sem afiliação com titulares de obras, personagens ou marcas de terceiros.
