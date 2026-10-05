namespace Buzzy.Core.Testes;

/// <summary>
/// Topologias de exemplo do desktop virtual para os testes do mundo do desktop
/// (TODO.md, Fase 1, "Testes automatizados"). Coordenadas em pixels físicos, com a origem
/// (0,0) no canto superior esquerdo do monitor principal; monitores à esquerda ou acima do
/// principal têm coordenadas negativas (ARCHITECTURE.md 2.4).
///
/// Convenções:
/// - Retângulos semiabertos, como o RECT do Windows: (esquerda, topo)-(direita, base).
/// - A barra de tarefas do Windows 11 tem 48 DIP: 48 px a 96 DPI, 60 px a 120 DPI, 72 px a
///   144 DPI e 96 px a 192 DPI. Salvo indicação, ela fica embaixo, em todos os monitores.
/// - Nos desenhos, <c>*</c> marca o monitor principal.
/// - Os números são literais de propósito: os testes comparam com valores calculados à mão
///   a partir deles, nunca com a mesma conta que o código faz.
///
/// Cada propriedade devolve uma instância nova. <see cref="Todas"/> lista todas com nome.
/// </summary>
internal static class TopologiasDeExemplo
{
    // Chaves no formato do nome GDI do dispositivo, como na Fase 1 (Topologia.cs).
    public const string Display1 = @"\\.\DISPLAY1";
    public const string Display2 = @"\\.\DISPLAY2";
    public const string Display3 = @"\\.\DISPLAY3";

    /// <summary>Um só monitor 1920x1080 a 96 DPI. O caso base.</summary>
    public static Topologia UmMonitor => new([
        Principal(Display1, Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1032), 96),
    ]);

    /// <summary>
    /// Dois monitores 1920x1080 lado a lado, alinhados pelo topo, secundário à direita.
    /// <code>
    /// [ 1* ][ 2  ]
    /// </code>
    /// </summary>
    public static Topologia LadoALado => new([
        Principal(Display1, Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1032), 96),
        Secundario(Display2, Ret(1920, 0, 3840, 1080), Ret(1920, 0, 3840, 1032), 96),
    ]);

    /// <summary>
    /// Empilhado, secundário acima: um 2560x1440 centralizado sobre o principal. Todo o
    /// secundário tem y negativo, e a esquerda dele também passa para x negativo.
    /// <code>
    /// [    2     ]
    ///   [  1*  ]
    /// </code>
    /// </summary>
    public static Topologia EmpilhadoSecundarioAcima => new([
        Principal(Display1, Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1032), 96),
        Secundario(Display2, Ret(-320, -1440, 2240, 0), Ret(-320, -1440, 2240, -48), 96),
    ]);

    /// <summary>
    /// Empilhado, secundário abaixo, alinhado pela esquerda.
    /// <code>
    /// [ 1* ]
    /// [ 2  ]
    /// </code>
    /// </summary>
    public static Topologia EmpilhadoSecundarioAbaixo => new([
        Principal(Display1, Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1032), 96),
        Secundario(Display2, Ret(0, 1080, 1920, 2160), Ret(0, 1080, 1920, 2112), 96),
    ]);

    /// <summary>
    /// Três monitores em L: principal no canto, um à direita e outro abaixo. O quadrante
    /// (1920,1080)-(3840,2160) fica vazio: é um vão no canto interno do L.
    /// <code>
    /// [ 1* ][ 2  ]
    /// [ 3  ]
    /// </code>
    /// </summary>
    public static Topologia EmL => new([
        Principal(Display1, Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1032), 96),
        Secundario(Display2, Ret(1920, 0, 3840, 1080), Ret(1920, 0, 3840, 1032), 96),
        Secundario(Display3, Ret(0, 1080, 1920, 2160), Ret(0, 1080, 1920, 2112), 96),
    ]);

    /// <summary>
    /// Secundário à esquerda, em x negativo: a máquina do usuário. Principal (0,0)-(1920,1080)
    /// com área útil (0,0)-(1920,1032); secundário (-1920,0)-(0,1080) com área útil
    /// (-1920,0)-(0,1032); ambos a 96 DPI.
    /// <code>
    /// [ 2  ][ 1* ]
    /// </code>
    /// </summary>
    public static Topologia SecundarioAEsquerda => new([
        Principal(Display1, Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1032), 96),
        Secundario(Display2, Ret(-1920, 0, 0, 1080), Ret(-1920, 0, 0, 1032), 96),
    ]);

    /// <summary>
    /// O principal não é o mais à esquerda nem o primeiro da lista: um 2560x1440 à esquerda,
    /// alinhado pela base, sobe 360 px acima do principal.
    /// <code>
    /// [    ]
    /// [ 2  ][ 1* ]
    /// </code>
    /// </summary>
    public static Topologia PrincipalADireita => new([
        Secundario(Display2, Ret(-2560, -360, 0, 1080), Ret(-2560, -360, 0, 1032), 96),
        Principal(Display1, Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1032), 96),
    ]);

    /// <summary>
    /// Monitor em retrato (1080x1920) à direita do principal, centralizado na vertical: sobe
    /// 420 px acima do topo do principal e desce 420 px abaixo da base.
    /// <code>
    ///       [  ]
    /// [ 1* ][2 ]
    ///       [  ]
    /// </code>
    /// </summary>
    public static Topologia Retrato => new([
        Principal(Display1, Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1032), 96),
        Secundario(Display2, Ret(1920, -420, 3000, 1500), Ret(1920, -420, 3000, 1452), 96),
    ]);

    /// <summary>
    /// Escalas mistas: principal 2560x1440 a 144 DPI (150%), 1920x1080 a 96 DPI (100%) à
    /// direita e 3840x2160 a 192 DPI (200%) à esquerda, todos alinhados pela base. A barra
    /// de tarefas tem 72, 48 e 96 px, respectivamente.
    /// <code>
    /// [      ]
    /// [  3   ][ 1* ]
    /// [      ][    ][ 2 ]
    /// </code>
    /// </summary>
    public static Topologia EscalasMistas => new([
        Principal(Display1, Ret(0, 0, 2560, 1440), Ret(0, 0, 2560, 1368), 144),
        Secundario(Display2, Ret(2560, 360, 4480, 1440), Ret(2560, 360, 4480, 1392), 96),
        Secundario(Display3, Ret(-3840, -720, 0, 1440), Ret(-3840, -720, 0, 1344), 192),
    ]);

    /// <summary>
    /// Monitores que não se tocam: um 1280x1024 separado do principal por um vão de 200 px
    /// (colunas 1920 a 2119) e 100 px mais baixo. O Windows não monta isso pela tela de
    /// configurações, mas o mundo do desktop precisa tolerar (ARCHITECTURE.md 2.13.3).
    /// <code>
    /// [ 1* ]  [ 2 ]
    /// </code>
    /// </summary>
    public static Topologia VaoEntreMonitores => new([
        Principal(Display1, Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1032), 96),
        Secundario(Display2, Ret(2120, 100, 3400, 1124), Ret(2120, 100, 3400, 1076), 96),
    ]);

    /// <summary>
    /// Três monitores desalinhados em degrau: cada um desce 400 px em relação ao anterior.
    /// Acima e abaixo dos degraus ficam regiões sem monitor.
    /// <code>
    /// [ 1* ]
    ///      [ 2  ]
    ///           [ 3  ]
    /// </code>
    /// </summary>
    public static Topologia DegrauDesalinhado => new([
        Principal(Display1, Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1032), 96),
        Secundario(Display2, Ret(1920, 400, 3840, 1480), Ret(1920, 400, 3840, 1432), 96),
        Secundario(Display3, Ret(3840, 800, 5760, 1880), Ret(3840, 800, 5760, 1832), 96),
    ]);

    /// <summary>
    /// Dois monitores que só se encostam pela quina (1920,1080): os pontos logo à direita do
    /// principal e logo abaixo dele caem em vãos.
    /// <code>
    /// [ 1* ]
    ///      [ 2  ]
    /// </code>
    /// </summary>
    public static Topologia QuinaComQuina => new([
        Principal(Display1, Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1032), 96),
        Secundario(Display2, Ret(1920, 1080, 3840, 2160), Ret(1920, 1080, 3840, 2112), 96),
    ]);

    /// <summary>Barra de tarefas vertical à esquerda, com 62 px: a área útil perde a faixa da esquerda.</summary>
    public static Topologia BarraAEsquerda => new([
        Principal(Display1, Ret(0, 0, 1920, 1080), Ret(62, 0, 1920, 1080), 96),
    ]);

    /// <summary>Barra de tarefas no topo, com 48 px: a área útil perde a faixa de cima.</summary>
    public static Topologia BarraNoTopo => new([
        Principal(Display1, Ret(0, 0, 1920, 1080), Ret(0, 48, 1920, 1080), 96),
    ]);

    /// <summary>Barra de tarefas vertical à direita, com 62 px: a área útil perde a faixa da direita.</summary>
    public static Topologia BarraADireita => new([
        Principal(Display1, Ret(0, 0, 1920, 1080), Ret(0, 0, 1858, 1080), 96),
    ]);

    /// <summary>Barra de tarefas que se esconde sozinha: a área útil é a tela inteira.</summary>
    public static Topologia BarraOculta => new([
        Principal(Display1, Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1080), 96),
    ]);

    /// <summary>
    /// Três monitores com o principal no meio da lista e do desktop: um 1920x1080 a 96 DPI
    /// à esquerda, 180 px mais baixo; o principal 2560x1440 a 120 DPI (125%); um 1920x1080
    /// a 120 DPI à direita, alinhado pelo topo. A barra tem 48 px a 96 DPI e 60 px a 120 DPI.
    /// <code>
    ///       [    ][ 3  ]
    /// [ 2  ][ 1* ]
    ///       [    ]
    /// </code>
    /// </summary>
    public static Topologia TresMonitores => new([
        Secundario(Display2, Ret(-1920, 180, 0, 1260), Ret(-1920, 180, 0, 1212), 96),
        Principal(Display1, Ret(0, 0, 2560, 1440), Ret(0, 0, 2560, 1380), 120),
        Secundario(Display3, Ret(2560, 0, 4480, 1080), Ret(2560, 0, 4480, 1020), 120),
    ]);

    /// <summary>Todas as topologias de exemplo, com o nome da propriedade.</summary>
    public static IReadOnlyList<(string Nome, Topologia Topologia)> Todas =>
    [
        (nameof(UmMonitor), UmMonitor),
        (nameof(LadoALado), LadoALado),
        (nameof(EmpilhadoSecundarioAcima), EmpilhadoSecundarioAcima),
        (nameof(EmpilhadoSecundarioAbaixo), EmpilhadoSecundarioAbaixo),
        (nameof(EmL), EmL),
        (nameof(SecundarioAEsquerda), SecundarioAEsquerda),
        (nameof(PrincipalADireita), PrincipalADireita),
        (nameof(Retrato), Retrato),
        (nameof(EscalasMistas), EscalasMistas),
        (nameof(VaoEntreMonitores), VaoEntreMonitores),
        (nameof(DegrauDesalinhado), DegrauDesalinhado),
        (nameof(QuinaComQuina), QuinaComQuina),
        (nameof(BarraAEsquerda), BarraAEsquerda),
        (nameof(BarraNoTopo), BarraNoTopo),
        (nameof(BarraADireita), BarraADireita),
        (nameof(BarraOculta), BarraOculta),
        (nameof(TresMonitores), TresMonitores),
    ];

    /// <summary>Retângulo semiaberto (esquerda, topo)-(direita, base).</summary>
    public static RetanguloPx Ret(int esquerda, int topo, int direita, int baseY) => new(esquerda, topo, direita, baseY);

    public static MonitorDoDesktop Principal(string chave, RetanguloPx tela, RetanguloPx areaUtil, int dpi)
        => new(chave, tela, areaUtil, dpi, Principal: true);

    public static MonitorDoDesktop Secundario(string chave, RetanguloPx tela, RetanguloPx areaUtil, int dpi)
        => new(chave, tela, areaUtil, dpi, Principal: false);

    /// <summary>A mesma topologia com os monitores em ordem inversa.</summary>
    public static Topologia Invertida(Topologia topologia) => new(topologia.Monitores.Reverse());

    /// <summary>
    /// A topologia com o monitor da chave trocado pelo resultado de <paramref name="mudar"/>,
    /// na mesma posição da lista.
    /// </summary>
    public static Topologia ComMonitor(Topologia topologia, string chave, Func<MonitorDoDesktop, MonitorDoDesktop> mudar)
        => new(topologia.Monitores.Select(m => m.Chave == chave ? mudar(m) : m));

    /// <summary>A topologia sem o monitor da chave.</summary>
    public static Topologia SemMonitor(Topologia topologia, string chave)
        => new(topologia.Monitores.Where(m => m.Chave != chave));

    // Mudanças da Fase 5 (topologia em execução, DEC-030). Ficam fora de Todas: entrar ali mudaria as contagens das
    // propriedades e as evidências do TODO.

    /// <summary>
    /// A topologia com outro monitor como principal e a origem (0,0) movida para ele, como o Windows faz ao trocar o
    /// principal: todos os monitores são transladados juntos, sem mudar de tamanho.
    /// </summary>
    public static Topologia Rebaseada(Topologia topologia, string novoPrincipal)
    {
        List<MonitorDoDesktop> lista = [.. topologia.Monitores];
        GeradorDeTopologias.Rebasear(lista, lista.FindIndex(m => m.Chave == novoPrincipal));
        return new Topologia(lista);
    }

    /// <summary>
    /// A topologia sem o monitor principal <paramref name="removido"/>, como quando ele é desconectado: outro assume e a
    /// origem vai para ele, transladando os que sobraram.
    /// </summary>
    public static Topologia SemOPrincipal(Topologia topologia, string removido, string novoPrincipal)
    {
        List<MonitorDoDesktop> lista = [.. topologia.Monitores.Where(m => m.Chave != removido)];
        GeradorDeTopologias.Rebasear(lista, lista.FindIndex(m => m.Chave == novoPrincipal));
        return new Topologia(lista);
    }

    /// <summary>
    /// A mesma topologia com as chaves trocadas e as mesmas telas, como a chave que passa da reserva <c>gdi:</c> para
    /// <c>mon:</c> quando a consulta de vídeo volta, ou o caminho do dispositivo que muda com o driver (DEC-030).
    /// </summary>
    public static Topologia ComChavesRenomeadas(Topologia topologia, Func<string, string> renomear)
        => new(topologia.Monitores.Select(m => m with { Chave = renomear(m.Chave) }));

    /// <summary>
    /// Três monitores 1920x1080 a 96 DPI em linha, com a barra de 48 px embaixo e o principal no meio.
    /// <code>
    /// [ 2  ][ 1* ][ 3  ]
    /// </code>
    /// </summary>
    public static Topologia TresEmLinhaComPrincipalNoMeio => new([
        Secundario(Display2, Ret(-1920, 0, 0, 1080), Ret(-1920, 0, 0, 1032), 96),
        Principal(Display1, Ret(0, 0, 1920, 1080), Ret(0, 0, 1920, 1032), 96),
        Secundario(Display3, Ret(1920, 0, 3840, 1080), Ret(1920, 0, 3840, 1032), 96),
    ]);
}
