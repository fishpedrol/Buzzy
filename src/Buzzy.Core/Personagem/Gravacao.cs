using System.Globalization;
using System.Text;

namespace Buzzy.Core.Personagem;

// Grava e reproduz sequências de eventos em texto (cultura invariante); os
// testes comparam a saída com arquivos de referência.
//
// Entrada: "> Press x=1632 y=1000", um evento por linha; vazias e "#" são
// comentário. Topologia vai pelo nome. "Tick vezes=N" aplica N passos;
// AutonomyTimer e ItemEffectTimer sem geração usam a agendada no momento.
//
// Saída: a linha do evento, depois "~" por transição, "!" por efeito,
// "x" por descarte e "=" com o retrato.
public static class Gravacao
{
    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    // Sem o prefixo ">".
    public static string Escrever(Evento evento, Func<Topologia, string> nomeDaTopologia)
    {
        ArgumentNullException.ThrowIfNull(evento);
        ArgumentNullException.ThrowIfNull(nomeDaTopologia);
        return evento switch
        {
            Press e => $"Press {Ponto(e.Cursor)}",
            DragMove e => $"DragMove {Ponto(e.Cursor)}",
            DragEnd e => $"DragEnd {Ponto(e.Cursor)}",
            ContextMenu e => $"ContextMenu {Ponto(e.Cursor)}",
            EnergySelected e => $"EnergySelected nivel={e.Nivel}",
            Loaded e => $"Loaded topologia={nomeDaTopologia(e.Topologia)} {DescreverPreferencias(e.Preferencias)}"
                + (e.PosicaoSalva is { } p ? $" posicao={DescreverPosicaoCompleta(p)}" : "") + DescreverPostura(e.Esconderijo, e.PresoPeloUsuario),
            TopologyChanged e => $"TopologyChanged topologia={nomeDaTopologia(e.Topologia)}",
            FullscreenTargetsChanged e => $"FullscreenTargetsChanged ocupados={e.Ocupados}",
            SettingsChanged e => $"SettingsChanged {DescreverPreferencias(e.Preferencias)}",
            MovementSignal e => $"MovementSignal sinal={e.Sinal}",
            AutonomyTimer e => string.Create(Invariante, $"AutonomyTimer geracao={e.Geracao}"),
            ItemEffectTimer e => string.Create(Invariante, $"ItemEffectTimer geracao={e.Geracao}"),
            ExpressionChange e => $"ExpressionChange expressao={e.Expressao}",
            CmdSetDominantEmotion e => $"CmdSetDominantEmotion emocao={e.Emocao?.ToString() ?? Automatica}",
            CmdSetAdultContent e => $"CmdSetAdultContent ligado={SimNao(e.Ligado)}",
            CmdSetAdultItemEnabled e => $"CmdSetAdultItemEnabled item={e.Item} ligado={SimNao(e.Ligado)}",
            CmdSetSelfUseItem e => $"CmdSetSelfUseItem item={e.Item} ligado={SimNao(e.Ligado)}",
            CmdSetFullscreenMode e => $"CmdSetFullscreenMode ligado={SimNao(e.Ligado)}",
            CmdSetCrossMonitors e => $"CmdSetCrossMonitors ligado={SimNao(e.Ligado)}",
            CmdSetEnergy e => $"CmdSetEnergy nivel={e.Nivel}",
            CmdSetAlwaysOnTop e => $"CmdSetAlwaysOnTop ligado={SimNao(e.Ligado)}",
            CmdSetScale e => $"CmdSetScale escala={e.Escala}",
            CmdSummonItem e => $"CmdSummonItem item={e.Item}",
            ItemPress e => string.Create(Invariante, $"ItemPress id={e.Id} {Ponto(e.Cursor)}"),
            ItemDragStart e => string.Create(Invariante, $"ItemDragStart id={e.Id}"),
            ItemDragMove e => string.Create(Invariante, $"ItemDragMove id={e.Id} {Ponto(e.Cursor)}"),
            ItemDragEnd e => string.Create(Invariante, $"ItemDragEnd id={e.Id} {Ponto(e.Cursor)}"),
            ItemRelease e => string.Create(Invariante, $"ItemRelease id={e.Id}"),
            _ => evento.GetType().Name,
        };
    }

    // Emoção dominante nula ("Automática").
    private const string Automatica = "Automatica";

    // Só "Tick vezes=N" devolve mais de um. O estado atual dá a geração dos
    // timers que vierem sem ela.
    public static IReadOnlyList<Evento> Ler(string linha, Func<string, Topologia> topologiaPorNome, EstadoDoNucleo atual)
    {
        ArgumentNullException.ThrowIfNull(linha);
        ArgumentNullException.ThrowIfNull(topologiaPorNome);
        ArgumentNullException.ThrowIfNull(atual);

        string[] partes = linha.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 0) throw new FormatException("Linha de evento vazia.");
        string nome = partes[0];
        var campos = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string parte in partes.Skip(1))
        {
            int igual = parte.IndexOf('=', StringComparison.Ordinal);
            if (igual <= 0) throw new FormatException($"Campo sem '=' em \"{linha}\": {parte}");
            if (!campos.TryAdd(parte[..igual], parte[(igual + 1)..]))
                throw new FormatException($"Campo repetido em \"{linha}\": {parte[..igual]}");
        }

        string Campo(string chave) => campos.TryGetValue(chave, out string? v) ? v : throw new FormatException($"Falta o campo {chave} em \"{linha}\".");
        PontoPx Cursor() => new(Inteiro(Campo("x")), Inteiro(Campo("y")));

        Evento Unico() => nome switch
        {
            "Press" => new Press(Cursor()),
            "Click" => new Click(),
            "DoubleClick" => new DoubleClick(),
            "DragStart" => new DragStart(),
            "DragMove" => new DragMove(Cursor()),
            "DragEnd" => new DragEnd(Cursor()),
            "DragCancel" => new DragCancel(),
            "ContextMenu" => new ContextMenu(Cursor()),
            "EnergyPanelOpen" => new EnergyPanelOpen(),
            "EnergySelected" => new EnergySelected(Enum.Parse<NivelDeEnergia>(Campo("nivel"))),
            "EnergyPanelClose" => new EnergyPanelClose(),
            "CmdHide" => new CmdHide(),
            "CmdShow" => new CmdShow(),
            "CmdPauseAutonomy" => new CmdPauseAutonomy(),
            "CmdResumeAutonomy" => new CmdResumeAutonomy(),
            "CmdOpenSettings" => new CmdOpenSettings(),
            "CmdResetPosition" => new CmdResetPosition(),
            "CmdExit" => new CmdExit(),
            "Loaded" => new Loaded(
                topologiaPorNome(Campo("topologia")),
                campos.TryGetValue("posicao", out string? p) ? LerPosicao(p) : null,
                LerPreferencias(campos))
            {
                Esconderijo = campos.TryGetValue("esconderijo", out string? lado) ? LerValor<LadoDoEsconderijo>("esconderijo", lado, "não é uma borda do esconderijo") : LadoDoEsconderijo.Nenhum,
                PresoPeloUsuario = campos.TryGetValue("preso", out string? preso) && SimOuNao("preso", preso),
            },
            "TopologyChanged" => new TopologyChanged(topologiaPorNome(Campo("topologia"))),
            "SessionLocked" => new SessionLocked(),
            "SessionUnlocked" => new SessionUnlocked(),
            "Suspending" => new Suspending(),
            "Resumed" => new Resumed(),
            "SessionEnding" => new SessionEnding(),
            "FullscreenTargetsChanged" => new FullscreenTargetsChanged(new MonitoresOcupados(
                Campo("ocupados").Split(',', StringSplitOptions.RemoveEmptyEntries))),
            "SettingsChanged" => new SettingsChanged(LerPreferencias(campos)),
            "Tick" => new Tick(),
            "MovementSignal" => new MovementSignal(Enum.Parse<SinalDeMovimento>(Campo("sinal"))),
            "AutonomyTimer" => new AutonomyTimer(campos.TryGetValue("geracao", out string? g) ? long.Parse(g, NumberStyles.Integer, Invariante) : atual.Geracao),
            "ItemEffectTimer" => new ItemEffectTimer(campos.TryGetValue("geracao", out string? go) ? long.Parse(go, NumberStyles.Integer, Invariante) : atual.GeracaoDaOnda),
            "ExpressionChange" => new ExpressionChange(Enum.Parse<Expressao>(Campo("expressao"))),
            "CmdSetDominantEmotion" => new CmdSetDominantEmotion(LerEmocao(Campo("emocao"))),
            "CmdSetAdultContent" => new CmdSetAdultContent(SimOuNao("ligado", Campo("ligado"))),
            "CmdSetAdultItemEnabled" => new CmdSetAdultItemEnabled(LerValor<Item>("item", Campo("item"), "não é um item"), SimOuNao("ligado", Campo("ligado"))),
            "CmdSetSelfUseItem" => new CmdSetSelfUseItem(LerValor<Item>("item", Campo("item"), "não é um item"), SimOuNao("ligado", Campo("ligado"))),
            "CmdSetFullscreenMode" => new CmdSetFullscreenMode(SimOuNao("ligado", Campo("ligado"))),
            "CmdSetCrossMonitors" => new CmdSetCrossMonitors(SimOuNao("ligado", Campo("ligado"))),
            "CmdSetEnergy" => new CmdSetEnergy(LerValor<NivelDeEnergia>("nivel", Campo("nivel"), "não é um nível de energia")),
            "CmdSetAlwaysOnTop" => new CmdSetAlwaysOnTop(SimOuNao("ligado", Campo("ligado"))),
            "CmdSetScale" => new CmdSetScale(LerValor<EscalaDoPersonagem>("escala", Campo("escala"), "não é uma escala")),
            "CmdSummonItem" => new CmdSummonItem(LerValor<Item>("item", Campo("item"), "não é um item")),
            "CmdClearItems" => new CmdClearItems(),
            "ItemPress" => new ItemPress(Inteiro(Campo("id")), Cursor()),
            "ItemDragStart" => new ItemDragStart(Inteiro(Campo("id"))),
            "ItemDragMove" => new ItemDragMove(Inteiro(Campo("id")), Cursor()),
            "ItemDragEnd" => new ItemDragEnd(Inteiro(Campo("id")), Cursor()),
            "ItemRelease" => new ItemRelease(Inteiro(Campo("id"))),
            _ => throw new FormatException($"Evento desconhecido: {nome}"),
        };

        if (nome == "Tick" && campos.TryGetValue("vezes", out string? vezes))
        {
            int n = Inteiro(vezes);
            if (n < 1) throw new FormatException($"Tick vezes={n}: precisa ser positivo.");
            return [.. Enumerable.Range(0, n).Select(_ => (Evento)new Tick())];
        }
        return [Unico()];
    }

    // Sem o prefixo "!".
    public static string DescreverEfeito(Efeito efeito)
    {
        ArgumentNullException.ThrowIfNull(efeito);
        return efeito switch
        {
            MoverJanela e => $"MoverJanela monitor={e.Destino.Monitor.Chave} ancora={Par(e.Destino.Ancora)} retangulo={Retangulo(e.Destino.Retangulo)}",
            AgendarDecisao e => string.Create(Invariante, $"AgendarDecisao atrasoMs={(long)e.Atraso.TotalMilliseconds} geracao={e.Geracao}"),
            AgendarOnda e => string.Create(Invariante, $"AgendarOnda atrasoMs={(long)e.Atraso.TotalMilliseconds} geracao={e.Geracao}"),
            MostrarItem e => string.Create(Invariante, $"MostrarItem id={e.Id} item={e.Item} monitor={e.Lugar.Monitor.Chave} ancora={Par(e.Lugar.Ancora)} retangulo={Retangulo(e.Lugar.Retangulo)}"),
            MoverItem e => string.Create(Invariante, $"MoverItem id={e.Id} monitor={e.Lugar.Monitor.Chave} ancora={Par(e.Lugar.Ancora)}"),
            EsconderItem e => string.Create(Invariante, $"EsconderItem id={e.Id}"),
            RemoverItem e => string.Create(Invariante, $"RemoverItem id={e.Id} motivo={e.Motivo}"),
            LiberarCapturaDoItem e => string.Create(Invariante, $"LiberarCapturaDoItem id={e.Id}"),
            AbrirMenu e => $"AbrirMenu ponto={Par(e.Ponto)}",
            GravarPosicao e => $"GravarPosicao posicao={DescreverPosicao(e.Posicao)}{DescreverPostura(e.Esconderijo, e.PresoPeloUsuario)}",
            GravarPreferencias e => $"GravarPreferencias {DescreverPreferencias(e.Preferencias)}",
            AplicarSempreNoTopo e => $"AplicarSempreNoTopo ligado={SimNao(e.Ligado)}",
            _ => efeito.GetType().Name,
        };
    }

    // Roda as linhas num núcleo novo e devolve a saída canônica.
    public static IReadOnlyList<string> Reproduzir(
        ConfiguracaoDoNucleo configuracao, ulong semente, IEnumerable<string> linhas, Func<string, Topologia> topologiaPorNome)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(linhas);
        ArgumentNullException.ThrowIfNull(topologiaPorNome);

        var nucleo = new Nucleo(configuracao, semente);
        var saida = new List<string>();
        foreach (string bruta in linhas)
        {
            string linha = bruta.Trim();
            if (!linha.StartsWith('>')) continue;
            string evento = linha[1..].Trim();
            saida.Add("> " + evento);

            foreach (Evento e in Ler(evento, topologiaPorNome, nucleo.Estado))
            {
                if (!nucleo.Enfileirar(e)) saida.Add($"x {Escrever(e, _ => "?")} descartado (autônomo com o usuário no controle)");
            }
            IReadOnlyList<Efeito> efeitos = nucleo.Processar((_, r) =>
            {
                foreach (Transicao t in r.Transicoes) saida.Add("~ " + t);
            });
            foreach (Efeito ef in efeitos) saida.Add("! " + DescreverEfeito(ef));
            saida.Add("= " + nucleo.Retrato.Descrever());
        }
        return saida;
    }

    // chave;fracaoX;fracaoY;ancoraX;ancoraY, sem a tela do monitor. Forma do
    // GravarPosicao nas reproduções.
    public static string DescreverPosicao(PosicaoDoPersonagem p)
    {
        ArgumentNullException.ThrowIfNull(p);
        return string.Create(Invariante, $"{p.ChaveMonitor};{p.FracaoX:0.######};{p.FracaoY:0.######};{p.AncoraAbsoluta.X};{p.AncoraAbsoluta.Y}");
    }

    // Com a tela do monitor, se conhecida: ...;esquerda;topo;direita;base.
    // Tela vazia conta como desconhecida (igual ao settings.json) e cai no
    // formato curto, pra tudo que sai daqui voltar por LerPosicao. Usado no
    // Loaded, pra reprodução restaurar pelo retângulo como a partida faz.
    public static string DescreverPosicaoCompleta(PosicaoDoPersonagem p)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (p.TelaDoMonitor is not { Vazio: false } t) return DescreverPosicao(p);
        return string.Create(Invariante, $"{DescreverPosicao(p)};{t.Esquerda};{t.Topo};{t.Direita};{t.Base}");
    }

    // 5 campos (sem tela) ou 9 (com tela, que não pode ser vazia).
    public static PosicaoDoPersonagem LerPosicao(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        string[] p = texto.Split(';');
        if (p.Length is not (5 or 9)) throw new FormatException($"Posição inválida (5 ou 9 campos): {texto}");
        var posicao = new PosicaoDoPersonagem(
            p[0],
            double.Parse(p[1], NumberStyles.Float, Invariante),
            double.Parse(p[2], NumberStyles.Float, Invariante),
            new PontoPx(Inteiro(p[3]), Inteiro(p[4])));
        if (p.Length == 5) return posicao;

        var tela = new RetanguloPx(Inteiro(p[5]), Inteiro(p[6]), Inteiro(p[7]), Inteiro(p[8]));
        if (tela.Vazio) throw new FormatException($"Tela do monitor vazia na posição: {texto}");
        return posicao with { TelaDoMonitor = tela };
    }

    // " esconderijo=X" e " preso=sim" só quando têm valor, pra não mudar as
    // linhas das referências gravadas antigas.
    private static string DescreverPostura(LadoDoEsconderijo esconderijo, bool preso)
        => (esconderijo != LadoDoEsconderijo.Nenhum ? $" esconderijo={esconderijo}" : "") + (preso ? " preso=sim" : "");

    // "energia=Media telaCheia=sim" e o resto só fora do valor antigo, pras
    // referências gravadas não mudarem. Por isso a ausência de adultos= vale
    // os nove e a de proprio= vale só o baseado, mesmo com o padrão atual
    // sendo outro.
    private static string DescreverPreferencias(Preferencias p)
        => $"energia={p.Energia} telaCheia={SimNao(p.ModoTelaCheia)}" + (p.AtravessarMonitores ? "" : " travessia=nao")
            + (p.EmocaoDominante is { } emocao ? $" emocao={emocao}" : "") + (p.ConteudoAdulto ? "" : " adulto=nao")
            + (p.SempreNoTopo ? "" : " topo=nao") + (p.Escala == EscalaDoPersonagem.Media ? "" : $" escala={p.Escala}")
            + (p.ItensAdultosHabilitados == Preferencias.TodosOsItensAdultos ? "" : $" adultos={DescreverItensAdultos(p.ItensAdultosHabilitados)}")
            + (p.ItensPorContaPropria == SoOBaseado ? "" : $" proprio={DescreverItensAdultos(p.ItensPorContaPropria)}");

    // Valor antigo do uso por conta própria.
    private static readonly ConjuntoDeItens SoOBaseado = ConjuntoDeItens.Vazio.Com(Item.Baseado);

    private static string DescreverItensAdultos(ConjuntoDeItens itens)
    {
        string[] nomes = [.. TabelaDoTamagotchi.Itens.Where(i => TabelaDoTamagotchi.Adulto(i) && itens.Contem(i)).Select(i => i.ToString())];
        return nomes.Length == 0 ? "nenhum" : string.Join(',', nomes);
    }

    private static ConjuntoDeItens LerItensAdultos(string valor)
    {
        ConjuntoDeItens itens = ConjuntoDeItens.Vazio;
        if (valor == "nenhum") return itens;
        foreach (string nome in valor.Split(','))
            itens = itens.Com(LerValor<Item>("adultos", nome, "não é um item"));
        return itens;
    }

    // Campo ausente vale o padrão.
    private static Preferencias LerPreferencias(Dictionary<string, string> campos) => new(
        campos.TryGetValue("energia", out string? e) ? Enum.Parse<NivelDeEnergia>(e) : Preferencias.Padrao.Energia,
        campos.TryGetValue("telaCheia", out string? t) ? SimOuNao("telaCheia", t) : Preferencias.Padrao.ModoTelaCheia,
        campos.TryGetValue("travessia", out string? a) ? SimOuNao("travessia", a) : true)
    {
        EmocaoDominante = campos.TryGetValue("emocao", out string? m) ? LerEmocao(m) : null,
        ConteudoAdulto = !campos.TryGetValue("adulto", out string? adulto) || SimOuNao("adulto", adulto),
        SempreNoTopo = !campos.TryGetValue("topo", out string? topo) || SimOuNao("topo", topo),
        Escala = campos.TryGetValue("escala", out string? escala) ? LerValor<EscalaDoPersonagem>("escala", escala, "não é uma escala") : EscalaDoPersonagem.Media,
        ItensAdultosHabilitados = campos.TryGetValue("adultos", out string? adultos) ? LerItensAdultos(adultos) : Preferencias.TodosOsItensAdultos,
        ItensPorContaPropria = campos.TryGetValue("proprio", out string? proprio) ? LerItensAdultos(proprio) : SoOBaseado,
    };

    // "Automatica" é a nula.
    private static Expressao? LerEmocao(string valor)
        => valor == Automatica ? null : LerValor<Expressao>("emocao", valor, $"não é uma expressão nem {Automatica}");

    // Aceita só o que Enum.ToString() escreveria: o nome exato, ou o número de
    // um valor fora do enum (os testes de saneamento gravam assim). Enum.Parse
    // aceitaria demais: "Feliz,Rindo" (viraria outro valor), número de valor
    // com nome, sinal e zeros à esquerda.
    private static T LerValor<T>(string campo, string valor, string motivo) where T : struct, Enum
    {
        foreach (T comNome in Enum.GetValues<T>())
        {
            if (string.Equals(comNome.ToString(), valor, StringComparison.Ordinal)) return comNome;
        }
        // Pelo número, só vale valor fora do enum e escrito do jeito canônico.
        if (int.TryParse(valor, NumberStyles.AllowLeadingSign, Invariante, out int numero))
        {
            var foraDoEnum = (T)Enum.ToObject(typeof(T), numero);
            if (string.Equals(foraDoEnum.ToString(), valor, StringComparison.Ordinal)) return foraDoEnum;
        }
        throw new FormatException($"{campo}={valor}: {motivo}.");
    }

    private static bool SimOuNao(string campo, string valor) => valor switch
    {
        "sim" => true,
        "nao" => false,
        _ => throw new FormatException($"{campo}={valor}: use sim ou nao."),
    };

    private static int Inteiro(string texto) => int.Parse(texto, NumberStyles.AllowLeadingSign, Invariante);

    private static string Ponto(PontoPx p) => string.Create(Invariante, $"x={p.X} y={p.Y}");

    private static string Par(PontoPx p) => string.Create(Invariante, $"({p.X},{p.Y})");

    private static string Retangulo(RetanguloPx r) => string.Create(Invariante, $"({r.Esquerda},{r.Topo})-({r.Direita},{r.Base})");

    private static string SimNao(bool valor) => valor ? "sim" : "nao";

    // Sempre \n, pra comparar e gravar as referências.
    public static string Juntar(IEnumerable<string> linhas)
    {
        ArgumentNullException.ThrowIfNull(linhas);
        var sb = new StringBuilder();
        foreach (string l in linhas) sb.Append(l).Append('\n');
        return sb.ToString();
    }
}
