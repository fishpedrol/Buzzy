using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Buzzy.App.Plataforma;
using Buzzy.App.Testes.Integracao;
using Buzzy.Core.Personagem;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Partes do adaptador que dá para verificar sem abrir janelas: tamanhos das estruturas
/// nativas (um campo errado faz o Windows recusar a chamada em silêncio, como aconteceu
/// no protótipo P1), textos da interface, o manifesto e o que o log pode levar.
/// </summary>
internal sealed class PlataformaTestes
{
    [Teste]
    public void EstruturasNativasTemOTamanhoQueOWindowsEspera()
    {
        Afirmar.Verdadeiro(Environment.Is64BitProcess, "os testes rodam em x64, como o aplicativo");
        Afirmar.Igual(104, Marshal.SizeOf<Win32.MONITORINFOEX>(), "MONITORINFOEXW");
        Afirmar.Igual(976, Marshal.SizeOf<Win32.NOTIFYICONDATA>(), "NOTIFYICONDATAW completa, com hBalloonIcon");
        Afirmar.Igual(40, Marshal.SizeOf<Win32.NOTIFYICONIDENTIFIER>(), "NOTIFYICONIDENTIFIER");
        Afirmar.Igual(16, Marshal.SizeOf<Win32.RECT>(), "RECT");
        Afirmar.Igual(8, Marshal.SizeOf<Win32.POINT>(), "POINT");
        // Menu com ícones (DEC-027): com o tamanho errado, InsertMenuItemW recusa o item e CreateDIBSection o bitmap.
        Afirmar.Igual(80, Marshal.SizeOf<Win32.MENUITEMINFO>(), "MENUITEMINFOW");
        Afirmar.Igual(40, Marshal.SizeOf<Win32.BITMAPINFOHEADER>(), "BITMAPINFOHEADER");

        // Configuração de vídeo, para a chave estável do monitor (DEC-030): com um tamanho errado, QueryDisplayConfig
        // e DisplayConfigGetDeviceInfo recusam o pedido, e toda chave cairia na reserva pelo nome GDI.
        Afirmar.Igual(8, Marshal.SizeOf<Win32.LUID>(), "LUID");
        Afirmar.Igual(8, Marshal.SizeOf<Win32.DISPLAYCONFIG_RATIONAL>(), "DISPLAYCONFIG_RATIONAL");
        Afirmar.Igual(20, Marshal.SizeOf<Win32.DISPLAYCONFIG_PATH_SOURCE_INFO>(), "DISPLAYCONFIG_PATH_SOURCE_INFO");
        Afirmar.Igual(48, Marshal.SizeOf<Win32.DISPLAYCONFIG_PATH_TARGET_INFO>(), "DISPLAYCONFIG_PATH_TARGET_INFO");
        Afirmar.Igual(72, Marshal.SizeOf<Win32.DISPLAYCONFIG_PATH_INFO>(), "DISPLAYCONFIG_PATH_INFO");
        Afirmar.Igual(64, Marshal.SizeOf<Win32.DISPLAYCONFIG_MODE_INFO>(), "DISPLAYCONFIG_MODE_INFO");
        Afirmar.Igual(20, Marshal.SizeOf<Win32.DISPLAYCONFIG_DEVICE_INFO_HEADER>(), "DISPLAYCONFIG_DEVICE_INFO_HEADER");
        Afirmar.Igual(84, Marshal.SizeOf<Win32.DISPLAYCONFIG_SOURCE_DEVICE_NAME>(), "DISPLAYCONFIG_SOURCE_DEVICE_NAME");
        Afirmar.Igual(420, Marshal.SizeOf<Win32.DISPLAYCONFIG_TARGET_DEVICE_NAME>(), "DISPLAYCONFIG_TARGET_DEVICE_NAME");
    }

    [Teste]
    public void CamposLidosDaConfiguracaoDeVideoEstaoNoDeslocamentoDoWindows()
    {
        // O tamanho certo não basta: dois campos trocados de lugar mantêm o tamanho e leriam o valor errado. Os
        // deslocamentos são os dos cabeçalhos do Windows (wingdi.h), nos campos que o Buzzy lê ou preenche.
        static int Em<T>(string campo) => (int)Marshal.OffsetOf<T>(campo);
        Afirmar.Igual(0, Em<Win32.DISPLAYCONFIG_PATH_INFO>(nameof(Win32.DISPLAYCONFIG_PATH_INFO.sourceInfo)), "PATH_INFO.sourceInfo");
        Afirmar.Igual(20, Em<Win32.DISPLAYCONFIG_PATH_INFO>(nameof(Win32.DISPLAYCONFIG_PATH_INFO.targetInfo)), "PATH_INFO.targetInfo");
        Afirmar.Igual(68, Em<Win32.DISPLAYCONFIG_PATH_INFO>(nameof(Win32.DISPLAYCONFIG_PATH_INFO.flags)), "PATH_INFO.flags");
        Afirmar.Igual(8, Em<Win32.DISPLAYCONFIG_PATH_SOURCE_INFO>(nameof(Win32.DISPLAYCONFIG_PATH_SOURCE_INFO.id)), "SOURCE_INFO.id");
        Afirmar.Igual(8, Em<Win32.DISPLAYCONFIG_PATH_TARGET_INFO>(nameof(Win32.DISPLAYCONFIG_PATH_TARGET_INFO.id)), "TARGET_INFO.id");
        Afirmar.Igual(40, Em<Win32.DISPLAYCONFIG_PATH_TARGET_INFO>(nameof(Win32.DISPLAYCONFIG_PATH_TARGET_INFO.targetAvailable)), "TARGET_INFO.targetAvailable");
        Afirmar.Igual(4, Em<Win32.DISPLAYCONFIG_DEVICE_INFO_HEADER>(nameof(Win32.DISPLAYCONFIG_DEVICE_INFO_HEADER.size)), "HEADER.size");
        Afirmar.Igual(8, Em<Win32.DISPLAYCONFIG_DEVICE_INFO_HEADER>(nameof(Win32.DISPLAYCONFIG_DEVICE_INFO_HEADER.adapterId)), "HEADER.adapterId");
        Afirmar.Igual(16, Em<Win32.DISPLAYCONFIG_DEVICE_INFO_HEADER>(nameof(Win32.DISPLAYCONFIG_DEVICE_INFO_HEADER.id)), "HEADER.id");
        Afirmar.Igual(20, Em<Win32.DISPLAYCONFIG_SOURCE_DEVICE_NAME>(nameof(Win32.DISPLAYCONFIG_SOURCE_DEVICE_NAME.viewGdiDeviceName)), "SOURCE_DEVICE_NAME.viewGdiDeviceName");
        Afirmar.Igual(164, Em<Win32.DISPLAYCONFIG_TARGET_DEVICE_NAME>(nameof(Win32.DISPLAYCONFIG_TARGET_DEVICE_NAME.monitorDevicePath)), "TARGET_DEVICE_NAME.monitorDevicePath");
    }

    [Teste]
    public void CaminhoDoDispositivoDoMonitorSoViraResumo()
    {
        // SECURITY.md 6 e DEC-030: o caminho do dispositivo identifica o hardware; o nome amigável e os códigos do EDID
        // dizem o modelo. No aplicativo, o caminho só é lido do pacote do Windows em ConfiguracaoDeVideo, que o entrega
        // dentro de um AlvoAtivo, e só ChavesDeMonitor lê o campo do alvo, para o resumo opaco; o resto da estrutura do
        // Windows nem é lido. Um uso novo, por exemplo num log, precisa entrar aqui de propósito.
        string app = Path.Combine(Caminhos.Raiz, "src", "Buzzy.App");
        string[] fontes = [.. Directory.GetFiles(app, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(app, f).Split(Path.DirectorySeparatorChar).Any(parte => parte is "bin" or "obj"))];
        string[] Onde(string termo) => [.. fontes
            .Where(f => Regex.IsMatch(File.ReadAllText(f), $@"\b{termo}\b"))
            .Select(f => Path.GetRelativePath(app, f))
            .Order(StringComparer.Ordinal)];

        Afirmar.Sequencia([@"Plataforma\ConfiguracaoDeVideo.cs", @"Plataforma\Win32.cs"], Onde("monitorDevicePath"), "quem lê o caminho do dispositivo");
        Afirmar.Sequencia([@"Plataforma\ChavesDeMonitor.cs"], Onde("CaminhoDoDispositivo"), "quem usa o caminho do dispositivo");
        foreach (string naoLido in new[] { "monitorFriendlyDeviceName", "edidManufactureId", "edidProductCodeId", "connectorInstance" })
            Afirmar.Sequencia([@"Plataforma\Win32.cs"], Onde(naoLido), $"{naoLido} só existe na declaração da estrutura");
    }

    [Teste]
    public void CoordenadasComSinalSaoPreservadas()
    {
        // Monitor à esquerda do principal: x negativo (ARCHITECTURE.md 2.13.3).
        nint lParam = unchecked((nint)((uint)(ushort)(short)-1500 | ((uint)(ushort)(short)-20 << 16)));
        Afirmar.Igual(-1500, Win32.XComSinal(lParam));
        Afirmar.Igual(-20, Win32.YComSinal(lParam));
        nint positivo = (nint)(300 | (200 << 16));
        Afirmar.Igual(300, Win32.XComSinal(positivo));
        Afirmar.Igual(200, Win32.YComSinal(positivo));
        Afirmar.Igual(0x0402, Win32.LoWord(unchecked((nint)0x0001_0402)));
        Afirmar.Igual(0x0001, Win32.HiWord(unchecked((nint)0x0001_0402)));
    }

    [Teste]
    public void TodosOsTextosExistemEmPortugues()
    {
        foreach (string chave in Textos.Chaves)
            Afirmar.Verdadeiro(!string.IsNullOrWhiteSpace(Textos.Obter(chave)), $"texto {chave}");

        Afirmar.Igual("&Esconder Buzzy", Textos.MenuEsconder);
        Afirmar.Igual("&Mostrar Buzzy", Textos.MenuMostrar);
        Afirmar.Igual("&Pausar movimento", Textos.MenuPausar);
        Afirmar.Igual("&Retomar movimento", Textos.MenuRetomar);
        Afirmar.Igual("&Sair", Textos.MenuSair);
        Afirmar.Igual("Buzzy", Textos.DicaDaBandeja);

        // Submenu da emoção dominante (DEC-027): "Automática" e as 14 caras de humor, na ordem de expressoes.png.
        Afirmar.Igual("Emoção &dominante", Textos.MenuEmocaoDominante);
        Afirmar.Igual("&Automática", Textos.MenuEmocaoAutomatica);
        Afirmar.Sequencia(
            ["&Neutro", "&Feliz", "&Rindo", "&Curioso", "&Surpreso", "Ass&ustado", "S&onolento",
             "&Bocejando", "&Dormindo", "&Travesso", "&Entediado", "&Pensativo", "E&mpolgado", "Determ&inado"],
            Expressoes.DeHumor.Select(Textos.Emocao), "as 14 emoções");
        foreach (Expressao efeito in Expressoes.DeEfeito)
            Afirmar.Lanca<ArgumentOutOfRangeException>(() => Textos.Emocao(efeito), $"{efeito} é cara de efeito, não emoção dominante");

        // Submenu dos itens do tamagotchi (DEC-028): só os nomes, na ordem do enum Item, e "Recolher itens".
        Afirmar.Igual("&Itens", Textos.MenuItens);
        Afirmar.Igual("&Recolher itens", Textos.MenuRecolherItens);
        Afirmar.Sequencia(
            ["&Banana", "Á&gua", "&Vodka", "&Cerveja", "Ba&seado", "C&igarro", "C&ocaína", "&MD", "&Lança-perfume",
             "Ca&fé", "E&nergético", "Cog&umelo", "B&ala"],
            Enum.GetValues<Item>().Select(Textos.Item), "os 13 itens");
        Afirmar.Lanca<ArgumentOutOfRangeException>(() => Textos.Item((Item)13), "um valor fora do enum não tem texto");
    }

    [Teste]
    public void ChavesSaoExatamenteAsDoTextosResx()
    {
        // Um texto novo no .resx sem entrar em Chaves (ou o contrário) escaparia do teste acima.
        XDocument resx = XDocument.Load(Path.Combine(Caminhos.Raiz, "src", "Buzzy.App", "Textos.resx"));
        string[] noArquivo = [.. resx.Root!.Elements("data").Select(d => (string)d.Attribute("name")!).Order(StringComparer.Ordinal)];
        Afirmar.Sequencia(noArquivo, Textos.Chaves.Order(StringComparer.Ordinal), "Textos.Chaves contra os nomes do Textos.resx");
        Afirmar.Igual(Textos.Chaves.Count, Textos.Chaves.Distinct(StringComparer.Ordinal).Count(), "chaves sem repetição");
    }

    [Teste]
    public void TeclasDeAcessoDoMenuNaoSeRepetem()
    {
        // O menu mostra um item de cada par (Esconder/Mostrar, Pausar/Retomar), o submenu da emoção dominante, com a chave
        // do tamagotchi o submenu "Itens", e Sair: as teclas de todas as combinações precisam ser diferentes, no menu
        // principal e em cada submenu (15 opções de emoção; 13 itens e "Recolher itens"). A verificação de tela usa E
        // (Esconder), S (Sair), D + letra (emoção) e I + letra (item).
        static char Tecla(string rotulo)
        {
            int e = rotulo.IndexOf('&', StringComparison.Ordinal);
            Afirmar.Verdadeiro(e >= 0 && e == rotulo.LastIndexOf('&') && e + 1 < rotulo.Length && char.IsAsciiLetter(rotulo[e + 1]),
                $"\"{rotulo}\": um & só, antes de uma letra sem acento (digitável sem tecla morta)");
            return char.ToUpperInvariant(rotulo[e + 1]);
        }

        foreach (bool tamagotchi in new[] { false, true })
        {
            foreach (bool visivel in new[] { true, false })
            {
                foreach (bool pausado in new[] { true, false })
                {
                    IReadOnlyList<EntradaDoMenu> menu = MenuNativo.Entradas(new ModeloDoMenu(visivel, pausado, null, AltoContraste: false, tamagotchi, ItensNaTela: 1));
                    char[] principal = [.. menu.Where(e => e.Tipo != TipoDeEntrada.Separador).Select(e => Tecla(e.Rotulo))];
                    Afirmar.Igual(principal.Length, principal.Distinct().Count(), $"teclas distintas no menu principal: [{string.Join(", ", principal)}]");
                    Afirmar.Igual(tamagotchi, principal.Contains('I'), "a tecla I é a do submenu \"Itens\", que só existe com a chave ligada");

                    EntradaDoMenu emocao = menu.Single(e => e.Tipo == TipoDeEntrada.Submenu && Tecla(e.Rotulo) == 'D');
                    char[] opcoes = [.. Afirmar.NaoNulo(emocao.Filhas).Where(e => e.Tipo != TipoDeEntrada.Separador).Select(e => Tecla(e.Rotulo))];
                    Afirmar.Igual(15, opcoes.Length, "Automática e as 14 emoções");
                    Afirmar.Igual(15, opcoes.Distinct().Count(), $"teclas distintas no submenu da emoção: [{string.Join(", ", opcoes)}]");

                    EntradaDoMenu[] itens = [.. menu.Where(e => e.Tipo == TipoDeEntrada.Submenu && Tecla(e.Rotulo) == 'I')];
                    Afirmar.Igual(tamagotchi ? 1 : 0, itens.Length, "o submenu \"Itens\" (I)");
                    if (itens.Length == 0) continue;
                    char[] doItem = [.. Afirmar.NaoNulo(itens[0].Filhas).Where(e => e.Tipo != TipoDeEntrada.Separador).Select(e => Tecla(e.Rotulo))];
                    Afirmar.Igual(14, doItem.Length, "os 13 itens e \"Recolher itens\"");
                    Afirmar.Igual(14, doItem.Distinct().Count(), $"teclas distintas no submenu dos itens: [{string.Join(", ", doItem)}]");
                }
            }
        }
        Afirmar.Igual(('E', 'S'), (Tecla(Textos.MenuEsconder), Tecla(Textos.MenuSair)), "as teclas que a verificação de tela usa");
        Afirmar.Igual(('I', 'B', 'R'), (Tecla(Textos.MenuItens), Tecla(Textos.Item(Item.Banana)), Tecla(Textos.MenuRecolherItens)),
            "I abre os itens, B invoca a banana e R recolhe");
    }

    [Teste]
    public void ManifestoDeclaraAsInvokerEPerMonitorV2()
    {
        string manifesto = File.ReadAllText(Path.Combine(Caminhos.Raiz, "src", "Buzzy.App", "app.manifest"));
        Afirmar.Contem("level=\"asInvoker\"", manifesto);
        Afirmar.Contem("uiAccess=\"false\"", manifesto);
        Afirmar.Contem(">PerMonitorV2<", manifesto);
    }

    [Teste]
    public void ExcecoesDoSistemaVaoParaOLogSoComTipoECodigo()
    {
        // SECURITY.md 6: a mensagem de uma exceção do Windows ou do .NET pode trazer um caminho, com o nome do usuário,
        // ou o SID da conta, no nome dos objetos da instância única; o log leva só o tipo e o HResult. As únicas
        // mensagens de exceção que o aplicativo usa são do próprio Buzzy: a da topologia incoerente (o construtor de
        // Topologia) e a dos pontos de teste do sprite (SpriteProvisorio). Uma nova precisa entrar aqui de propósito.
        string app = Path.Combine(Caminhos.Raiz, "src", "Buzzy.App");
        string[] usos = [.. Directory.GetFiles(app, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(app, f).Split(Path.DirectorySeparatorChar).Any(parte => parte is "bin" or "obj"))
            .SelectMany(f => File.ReadAllLines(f).Where(l => Regex.IsMatch(l, @"\.Message\b")).Select(l => $"{Path.GetRelativePath(app, f)}: {l.Trim()}"))
            .Order(StringComparer.Ordinal)];
        Afirmar.Sequencia(
            [
                @"Composicao\Aplicacao.cs: Diagnostico.Evento(""ERRO"", (""etapa"", ""pontos de teste do sprite""), (""mensagem"", e.Message));",
                @"Plataforma\LeitorDeTopologia.cs: erro = e.Message;",
            ],
            usos, "mensagens de exceção usadas no aplicativo");
    }

    [Teste]
    public void PastaDeDadosFicaNoLocalAppDataDoUsuario()
    {
        string esperado = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buzzy");
        Afirmar.Igual(esperado, Diagnostico.PastaDeDados());
    }

    [Teste]
    public void OProcessoDeTestesRodaEmPerMonitorV2PeloProprioManifesto()
    {
        // Sem janela: só consulta o contexto de DPI da thread. Prova que o app.manifest dos
        // testes foi embutido e vale, e que as coordenadas lidas pelos testes de integração
        // são físicas, como as do log do Buzzy.
        Afirmar.Verdadeiro(NativoTeste.ThreadEmPerMonitorV2(), "a thread de testes está em Per-Monitor V2");
    }

    [Teste]
    public void ManifestosDasFerramentasDeTesteDeclaramAsInvokerEPerMonitorV2()
    {
        foreach (string projeto in new[] { "Buzzy.App.Testes", "Buzzy.Verificacao" })
        {
            string pasta = Path.Combine(Caminhos.Raiz, "tests", projeto);
            string manifesto = File.ReadAllText(Path.Combine(pasta, "app.manifest"));
            Afirmar.Contem("level=\"asInvoker\"", manifesto, projeto);
            Afirmar.Contem("uiAccess=\"false\"", manifesto, projeto);
            Afirmar.Contem(">PerMonitorV2<", manifesto, projeto);
            Afirmar.Contem(">true/pm<", manifesto, projeto);
            Afirmar.Contem("<ApplicationManifest>app.manifest</ApplicationManifest>", File.ReadAllText(Path.Combine(pasta, projeto + ".csproj")), projeto);
        }
    }
}
