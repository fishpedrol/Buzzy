using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Buzzy.Visual.Pixel;

namespace Buzzy.Identidade;

// Prévias em assets/identidade/pixel/previa/, ampliadas sem suavização. Tamanho real é 2×
// (128 DIP a 100%).
internal static class PreviaPixel
{
    private static readonly uint Fundo = 0xFFF1EEE8;
    private static readonly uint FundoEscuro = 0xFF23252B;

    // Fundo aproximado do menu nativo do Windows 11, claro e escuro.
    private static readonly uint FundoDoMenu = 0xFFF9F9F9;
    private static readonly uint FundoDoMenuEscuro = 0xFF2C2C2C;

    internal static int Gerar(string raiz, IReadOnlyList<PosePixel> poses)
    {
        string pasta = Path.Combine(raiz, "assets", "identidade", "pixel", "previa");
        Directory.CreateDirectory(pasta);

        int problemas = 0;
        foreach (PosePixel pose in poses.Concat(PosesPixel.DosGestos).Concat(PosesPixel.DosClipes))
        {
            foreach (string expressao in Rostos.Expressoes.Keys)
            {
                // O cipó encosta na borda de cima de propósito (é onde se prende), então confere sem ele.
                Tela t = BonecoPixel.Desenhar(pose with { Cipo = null }, expressao);
                if (t.Limites() is not { } l) continue;
                // Só a cara da própria pose e o chapéu torto do bêbado. O chapéu eriçado encosta no topo
                // em andando-2/4 e escalando-1/2 com qualquer cara, mas o contorno fica inteiro.
                if (Encosta(l) && (expressao == pose.Expressao || Rostos.Expressoes[expressao].Topete == Topete.Torto))
                {
                    Console.WriteLine($"  NA BORDA DO QUADRO: pose '{pose.Nome}' com '{expressao}' ocupa ({l.Esquerda},{l.Topo})-({l.Direita},{l.Base})");
                    problemas++;
                }
                // Preenchimento na borda de cima ou dos lados corta o contorno, com qualquer cara.
                if (PreenchimentoNaBorda(t) is { } p)
                {
                    Console.WriteLine($"  CONTORNO CORTADO NA BORDA: pose '{pose.Nome}' com '{expressao}' tem {t[p.X, p.Y]} em ({p.X},{p.Y})");
                    problemas++;
                }
            }
        }
        problemas += ConferirItens();
        problemas += ConferirUsos();
        problemas += ConferirEfeitos([.. poses, .. PosesPixel.DosGestos, .. PosesPixel.DosClipes]);

        PosePixel parado = poses.First(p => p.Nome == "parado");
        Salvar(Ampliada(BonecoPixel.Desenhar(parado), 8, Fundo), Path.Combine(pasta, "parado-8x.png"));
        Salvar(Ampliada(Icone.Desenhar(), 16, Fundo), Path.Combine(pasta, "icone-16x.png"));
        Salvar(Ampliada(BonecoPixel.Desenhar(poses.First(p => p.Nome == "andando-1")), 8, Fundo), Path.Combine(pasta, "andando-8x.png"));
        Salvar(Grade([.. poses.Select(p => (p.Nome, BonecoPixel.Desenhar(p)))], 4, 6, Fundo), Path.Combine(pasta, "poses-claro.png"));
        Salvar(Grade([.. poses.Select(p => (p.Nome, BonecoPixel.Desenhar(p)))], 4, 6, FundoEscuro), Path.Combine(pasta, "poses-escuro.png"));
        // As 14 caras de humor; cada célula é o ícone do menu (recorte 40×32 em (12, 0) do parado).
        Salvar(Grade([.. Rostos.DeHumor.Select(x => (x, IconesDoMenu.Rosto(x)))], 8, 7, Fundo), Path.Combine(pasta, "expressoes.png"));
        // Caras de efeito, passageiras e de efeito de perfil (andando). O recorte de perfil para uma
        // linha antes porque na linha 31 aparece a ponta da cauda.
        PosePixel andando = poses.First(p => p.Nome == "andando-2");
        int xDoPerfil = (int)Math.Round(BonecoPixel.Pontos(andando).Cabeca.X) - 18;
        Salvar(Grade(
            [
                .. Rostos.DeEfeito.Select(x => (x, IconesDoMenu.Rosto(x))),
                .. Rostos.Passageiras.Select(x => (x, IconesDoMenu.Rosto(x))),
                .. Rostos.DeEfeito.Select(x => ($"{x} (perfil)", BonecoPixel.Desenhar(andando, x).Recortada(xDoPerfil, IconesDoMenu.YDoRosto, IconesDoMenu.LarguraDoRosto, IconesDoMenu.AlturaDoRosto - 1))),
            ], 8, 7, Fundo), Path.Combine(pasta, "rostos-efeito.png"));
        Salvar(Grade([.. poses.Select(p => (p.Nome, BonecoPixel.Desenhar(p)))], 2, 10, Fundo), Path.Combine(pasta, "tamanho-real.png"));

        Salvar(Grade([.. ItensPixel.Todos.Select(i => (i, ItensPixel.Desenhar(i)))], 8, 7, Fundo), Path.Combine(pasta, "itens-8x.png"));
        Salvar(ItensNaMao(), Path.Combine(pasta, "itens-na-mao-8x.png"));
        Salvar(ItensAoLado(parado), Path.Combine(pasta, "itens-tamanho-real.png"));
        Salvar(Usos(), Path.Combine(pasta, "usos.png"));
        Salvar(UsosEmTamanhoReal(), Path.Combine(pasta, "usos-tamanho-real.png"));
        Salvar(Efeitos(poses), Path.Combine(pasta, "efeitos.png"));
        Salvar(Gestos(), Path.Combine(pasta, "gestos.png"));
        // Cada quadro de clipe ao lado da pose-chave de onde saiu.
        Salvar(Grade([.. PosesPixel.DosClipes.SelectMany(q => new[] { (Chave(q).Nome, BonecoPixel.Desenhar(Chave(q))), (q.Nome, BonecoPixel.Desenhar(q)) })], 4, 4, Fundo), Path.Combine(pasta, "clipes.png"));
        // Reações ao clique, cada pose+cara distinta dos clipes do app, pra ver se a cara se lê com
        // os braços erguidos.
        var manifesto = Buzzy.Visual.Animacao.ManifestoDeClipes.Ler(File.ReadAllText(Path.Combine(raiz, "src", "Buzzy.App", "Apresentacao", "clipes.json")));
        Salvar(Grade([.. Buzzy.Visual.Animacao.Situacoes.Reacoes.SelectMany(v => manifesto[Buzzy.Visual.Animacao.Situacoes.DaReacao(v)].Quadros
            .Select(q => (q.Pose, q.Cara)).Distinct().Select(q => ($"{v}: {q.Pose}/{q.Cara}", BonecoPixel.Desenhar(PosesPixel.PorNome(q.Pose)!, q.Cara))))], 4, 4, Fundo), Path.Combine(pasta, "reacoes.png"));
        // Explorar a borda: virado pra direita (nativo) e pra esquerda (espelhado).
        Salvar(Grade([.. new[] { "andando-2-espia1", "andando-2-espia2", "andando-2-olha1" }.SelectMany(n => new[] { ($"{n}, direita", BonecoPixel.Desenhar(PosesPixel.PorNome(n)!)), ($"{n}, esquerda", BonecoPixel.Desenhar(PosesPixel.PorNome(n)!).Espelhada()) })], 4, 5, Fundo), Path.Combine(pasta, "borda.png"));
        Salvar(Paranoico(andando, xDoPerfil), Path.Combine(pasta, "paranoico-8x.png"));
        Salvar(IconesDoMenuNativo(), Path.Combine(pasta, "icones-menu.png"));

        // A folha nativa (64×64 por quadro) é a que o app usa; a ampliação é só prévia.
        string folha = Path.Combine(raiz, "assets", "identidade", "pixel", "buzzy-poses.png");
        Salvar(Grade([.. poses.Select(p => (p.Nome, BonecoPixel.Desenhar(p)))], 1, poses.Count, 0x00000000, rotulos: false), folha);
        // Itens: 24×24 cada, na ordem do menu.
        string folhaDosItens = Path.Combine(raiz, "assets", "identidade", "pixel", "buzzy-itens.png");
        Salvar(Grade([.. ItensPixel.Todos.Select(i => (i, ItensPixel.Desenhar(i)))], 1, ItensPixel.Todos.Count, 0x00000000, rotulos: false), folhaDosItens);
        return problemas;
    }

    private static bool Encosta((int Esquerda, int Topo, int Direita, int Base) l)
        => l.Esquerda <= 0 || l.Topo <= 0 || l.Direita >= BonecoPixel.Lado || l.Base > BonecoPixel.Lado;

    // Pixel que não é nada nem contorno na borda de cima ou dos lados. A de baixo é o chão, pode.
    private static (int X, int Y)? PreenchimentoNaBorda(Tela t)
    {
        for (int i = 0; i < t.Largura; i++)
            foreach ((int x, int y) in new[] { (i, 0), (0, i), (t.Largura - 1, i) })
                if (t[x, y] is not Cor.Nada and not Cor.Contorno) return (x, y);
        return null;
    }

    // "cocando-2" vem de "cocando".
    private static PosePixel Chave(PosePixel quadro)
    {
        string nome = quadro.Nome[..quadro.Nome.LastIndexOf('-')];
        return PosesPixel.Todas.First(p => p.Nome == nome);
    }

    // Mesmo recorte do ícone do menu.
    private static Tela RecorteDoRosto(Tela quadro)
        => quadro.Recortada(IconesDoMenu.XDoRosto, IconesDoMenu.YDoRosto, IconesDoMenu.LarguraDoRosto, IconesDoMenu.AlturaDoRosto);

    // Item pousa na última linha, com 1 px livre no topo e nas laterais.
    private static int ConferirItens()
    {
        int problemas = 0;
        foreach (string item in ItensPixel.Todos)
        {
            if (ItensPixel.Desenhar(item).Limites() is not { } l) continue;
            bool certo = l.Esquerda >= 1 && l.Topo >= 1 && l.Direita <= ItensPixel.Lado - 1 && l.Base == ItensPixel.Lado;
            if (!certo)
            {
                Console.WriteLine($"  ITEM FORA DA GRADE: '{item}' ocupa ({l.Esquerda},{l.Topo})-({l.Direita},{l.Base})");
                problemas++;
            }
        }
        return problemas;
    }

    // Todo quadro de uso, com cada item, espelhado ou não, longe das bordas.
    private static int ConferirUsos()
    {
        int problemas = 0;
        foreach (Verbo verbo in Enum.GetValues<Verbo>())
        {
            foreach (string item in ItensPixel.DoVerbo(verbo))
            {
                foreach (PosePixel pose in PosesDoUso(verbo))
                {
                    foreach (bool espelhado in new[] { false, true })
                    {
                        Tela t = BonecoPixel.Desenhar(pose, null, item);
                        if (espelhado) t = t.Espelhada();
                        if (t.Limites() is { } l && Encosta(l))
                        {
                            Console.WriteLine($"  USO NA BORDA DO QUADRO: '{pose.Nome}' com '{item}'{(espelhado ? " espelhado" : "")} ocupa ({l.Esquerda},{l.Topo})-({l.Direita},{l.Base})");
                            problemas++;
                        }
                    }
                }
            }
        }
        return problemas;
    }

    // Efeito nenhum (em fase nenhuma, com o modificador de pose) pode levar o desenho a uma borda
    // que a pose sem efeito não toca.
    private static int ConferirEfeitos(IReadOnlyList<PosePixel> poses)
    {
        int problemas = 0;
        foreach (PosePixel pose in poses)
        {
            foreach (EfeitoVisual efeito in EfeitosPixel.Todos)
            {
                foreach (string cara in new[] { pose.Expressao, CaraDoEfeito(efeito) ?? pose.Expressao })
                {
                    if (BonecoPixel.Desenhar(pose with { Cipo = null }, cara).Limites() is not { } l0) continue;
                    for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
                    {
                        PosePixel m = EfeitosPixel.Modificar(pose, efeito, fase) with { Cipo = null };
                        if (BonecoPixel.Desenhar(m, cara, null, efeito, fase).Limites() is not { } l) continue;
                        bool piora = (l.Esquerda <= 0 && l0.Esquerda > 0) || (l.Topo <= 0 && l0.Topo > 0) || (l.Direita >= BonecoPixel.Lado && l0.Direita < BonecoPixel.Lado);
                        if (piora)
                        {
                            Console.WriteLine($"  EFEITO NA BORDA DO QUADRO: '{pose.Nome}' com '{cara}', {efeito} fase {fase}, ocupa ({l.Esquerda},{l.Topo})-({l.Direita},{l.Base})");
                            problemas++;
                        }
                    }
                }
            }
        }
        return problemas;
    }

    // A cara que acompanha o efeito na onda; nula nos que são só de uso.
    private static string? CaraDoEfeito(EfeitoVisual efeito) => efeito switch
    {
        EfeitoVisual.Fumaca => "chapado",
        EfeitoVisual.Bolhas => "bebado",
        EfeitoVisual.Brilhos => "eletrico",
        EfeitoVisual.Estrelinhas => "tonto",
        EfeitoVisual.Coracoes => "apaixonado",
        EfeitoVisual.Cores => "viajando",
        EfeitoVisual.Suor => "paranoico",
        _ => null,
    };

    // Sem repetição, na ordem da animação.
    private static List<PosePixel> PosesDoUso(Verbo verbo) => [.. UsosPixel.Sequencia(verbo).Select(q => q.Pose).DistinctBy(p => p.Nome)];

    // Cada variante de item na mão (em pé, no gole, deitada na tragada...), já contornada.
    // Quadrado ciano marca a pega; ponto magenta, a ponta.
    private static BitmapSource ItensNaMao()
    {
        const int lado = 16;
        var celulas = new List<(string Nome, Tela Tela, (int X, int Y) Pega, (int X, int Y)? Ponta)>();
        foreach (string item in ItensPixel.Todos)
        {
            foreach (string variante in ItensPixel.VariantesNaMao(item))
            {
                ItemNaMao m = ItensPixel.NaMao(item, variante);
                var t = new Tela(lado, lado);
                int x0 = (lado - m.Desenho.Largura) / 2, y0 = Math.Min((lado - m.Desenho.Altura) / 2, lado - 2 - m.Pega.Y);
                t.Carimbar(m.Desenho, x0, y0);
                t.Contornar(Cor.Contorno);
                celulas.Add(($"{item}\n{variante}", t, (m.Pega.X + x0, m.Pega.Y + y0), m.Ponta is { } p ? (p.X + x0, p.Y + y0) : null));
            }
        }

        const int escala = 8, colunas = 8, rotulo = 34;
        BitmapSource grade = Grade([.. celulas.Select(c => (c.Nome, c.Tela))], escala, colunas, Fundo, alturaDoRotulo: rotulo);
        const int margem = 10, cw = lado * escala, ch = lado * escala;
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawImage(grade, new Rect(0, 0, grade.PixelWidth, grade.PixelHeight));
            var ciano = new Pen(new SolidColorBrush(Color.FromRgb(0x00, 0xB8, 0xD4)), 2);
            var magenta = new SolidColorBrush(Color.FromRgb(0xE0, 0x1E, 0xC8));
            for (int i = 0; i < celulas.Count; i++)
            {
                int x = margem + i % colunas * (cw + margem), y = margem + i / colunas * (ch + margem + rotulo);
                (int px, int py) = celulas[i].Pega;
                dc.DrawRectangle(null, ciano, new Rect(x + px * escala + 1, y + py * escala + 1, escala - 2, escala - 2));
                if (celulas[i].Ponta is { } b)
                    dc.DrawEllipse(magenta, null, new Point(x + b.X * escala + escala / 2.0, y + b.Y * escala + escala / 2.0), 2.5, 2.5);
            }
        }
        return Renderizar(visual, grade.PixelWidth, grade.PixelHeight);
    }

    // Por verbo: a sequência com os passos de cada quadro e uma linha por item, a 2× (tamanho na tela).
    private static BitmapSource Usos()
    {
        const int escala = 2, celula = 64 * escala, folga = 6, colunaDoNome = 96, margem = 12, titulo = 22, rotulo = 16;
        Verbo[] verbos = Enum.GetValues<Verbo>();
        int maxQuadros = verbos.Max(v => PosesDoUso(v).Count);
        int largura = margem + colunaDoNome + maxQuadros * (celula + folga) + margem;
        int altura = margem + verbos.Sum(v => titulo + rotulo + ItensPixel.DoVerbo(v).Count * (celula + folga) + margem);
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(ParaCor(Fundo)), null, new Rect(0, 0, largura, altura));
            int y = margem;
            foreach (Verbo verbo in verbos)
            {
                IReadOnlyList<QuadroDeUso> sequencia = UsosPixel.Sequencia(verbo);
                string passos = string.Join(" · ", sequencia.Select(q => $"{q.Pose.Nome[(q.Pose.Nome.LastIndexOf('-') + 1)..]}:{q.Passos}"));
                Texto(dc, $"{verbo.ToString().ToLowerInvariant()} — {UsosPixel.Passos(verbo)} passos ({UsosPixel.Passos(verbo) / 60.0:0.##} s): {passos}", margem, y, 13, true);
                y += titulo;
                List<PosePixel> quadros = PosesDoUso(verbo);
                for (int i = 0; i < quadros.Count; i++)
                    Texto(dc, $"{quadros[i].Nome} ({quadros[i].Expressao})", margem + colunaDoNome + i * (celula + folga), y, 10, false);
                y += rotulo;
                foreach (string item in ItensPixel.DoVerbo(verbo))
                {
                    Texto(dc, item, margem, y + celula / 2 - 8, 13, false);
                    for (int i = 0; i < quadros.Count; i++)
                    {
                        BitmapSource b = Ampliada(BonecoPixel.Desenhar(quadros[i], null, item), escala, Fundo);
                        dc.DrawImage(b, new Rect(margem + colunaDoNome + i * (celula + folga), y, celula, celula));
                    }
                    y += celula + folga;
                }
                y += margem;
            }
        }
        return Renderizar(visual, largura, altura);
    }

    // Os mesmos quadros a 1×, fundo claro e escuro, sem rótulo.
    private static BitmapSource UsosEmTamanhoReal()
    {
        const int folga = 4, margem = 8;
        Verbo[] verbos = Enum.GetValues<Verbo>();
        var linhas = verbos.SelectMany(v => ItensPixel.DoVerbo(v).Select(item => (Item: item, Quadros: PosesDoUso(v)))).ToList();
        int maxQuadros = linhas.Max(l => l.Quadros.Count);
        int metade = margem + maxQuadros * (64 + folga) + margem;
        int largura = metade * 2, altura = margem + linhas.Count * (64 + folga) + margem;
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(ParaCor(Fundo)), null, new Rect(0, 0, metade, altura));
            dc.DrawRectangle(new SolidColorBrush(ParaCor(FundoEscuro)), null, new Rect(metade, 0, metade, altura));
            for (int j = 0; j < linhas.Count; j++)
            {
                for (int i = 0; i < linhas[j].Quadros.Count; i++)
                {
                    Tela t = BonecoPixel.Desenhar(linhas[j].Quadros[i], null, linhas[j].Item);
                    foreach ((int x0, uint fundo) in new[] { (0, Fundo), (metade, FundoEscuro) })
                        dc.DrawImage(Ampliada(t, 1, fundo), new Rect(x0 + margem + i * (64 + folga), margem + j * (64 + folga), 64, 64));
                }
            }
        }
        return Renderizar(visual, largura, altura);
    }

    // 8 efeitos x 3 fases (a 0 é parada) no parado, andando e sentado, a 2×, com a cara da onda.
    private static BitmapSource Efeitos(IReadOnlyList<PosePixel> poses)
    {
        string[] nomes = ["parado", "andando-2", "sentado"];
        var celulas = new List<(string, Tela)>();
        foreach (EfeitoVisual efeito in EfeitosPixel.Todos)
        {
            foreach (string nome in nomes)
            {
                PosePixel pose = poses.First(p => p.Nome == nome);
                for (int fase = 0; fase < EfeitosPixel.Fases; fase++)
                {
                    PosePixel m = EfeitosPixel.Modificar(pose, efeito, fase);
                    celulas.Add(($"{efeito} {fase} · {nome}", BonecoPixel.Desenhar(m, CaraDoEfeito(efeito) ?? pose.Expressao, null, efeito, fase)));
                }
            }
        }
        return Grade(celulas, 2, nomes.Length * EfeitosPixel.Fases, Fundo);
    }

    // Gestos da onda: em cima a 4× com a cara própria; embaixo a 2× com o efeito da onda em que o
    // gesto mais aparece (fase 0).
    private static BitmapSource Gestos()
    {
        var onda = new Dictionary<string, EfeitoVisual>
        {
            ["soluco"] = EfeitoVisual.Bolhas,
            ["danca"] = EfeitoVisual.Coracoes,
            ["gargalhada"] = EfeitoVisual.Fumaca,
            ["espirro"] = EfeitoVisual.Brilhos,
            ["tosse"] = EfeitoVisual.Nenhum,
            ["tremedeira"] = EfeitoVisual.Brilhos,
            ["olharproteto"] = EfeitoVisual.Suor,
            ["agachar"] = EfeitoVisual.Suor,
        };
        BitmapSource proprias = Grade([.. PosesPixel.DosGestos.Select(p => ($"{p.Nome} ({p.Expressao})", BonecoPixel.Desenhar(p)))], 4, PosesPixel.DosGestos.Count, Fundo);
        BitmapSource naOnda = Grade([.. PosesPixel.DosGestos.Select(p =>
            ($"+ {onda[p.Nome]}", BonecoPixel.Desenhar(EfeitosPixel.Modificar(p, onda[p.Nome], 0), null, null, onda[p.Nome], 0)))], 2, PosesPixel.DosGestos.Count, Fundo);
        var visual = new DrawingVisual();
        int largura = Math.Max(proprias.PixelWidth, naOnda.PixelWidth), altura = proprias.PixelHeight + naOnda.PixelHeight;
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(ParaCor(Fundo)), null, new Rect(0, 0, largura, altura));
            dc.DrawImage(proprias, new Rect(0, 0, proprias.PixelWidth, proprias.PixelHeight));
            dc.DrawImage(naOnda, new Rect(0, proprias.PixelHeight, naOnda.PixelWidth, naOnda.PixelHeight));
        }
        return Renderizar(visual, largura, altura);
    }

    // Paranoia de perto, a 8×: a cara de frente e de perfil em cima; embaixo "olharproteto" e
    // "agachar" com suor na fase parada.
    private static BitmapSource Paranoico(PosePixel andando, int xDoPerfil)
    {
        BitmapSource rostos = Grade(
        [
            ("paranoico", IconesDoMenu.Rosto("paranoico")),
            ("paranoico (perfil)", BonecoPixel.Desenhar(andando, "paranoico").Recortada(xDoPerfil, IconesDoMenu.YDoRosto, IconesDoMenu.LarguraDoRosto, IconesDoMenu.AlturaDoRosto - 1)),
        ], 8, 2, Fundo);
        BitmapSource gestos = Grade([.. new[] { "olharproteto", "agachar" }.Select(nome => PosesPixel.PorNome(nome)!).Select(p =>
            ($"{p.Nome} + {EfeitoVisual.Suor} 0", BonecoPixel.Desenhar(EfeitosPixel.Modificar(p, EfeitoVisual.Suor, 0), null, null, EfeitoVisual.Suor, 0)))], 8, 2, Fundo);
        var visual = new DrawingVisual();
        int largura = Math.Max(rostos.PixelWidth, gestos.PixelWidth), altura = rostos.PixelHeight + gestos.PixelHeight;
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(ParaCor(Fundo)), null, new Rect(0, 0, largura, altura));
            dc.DrawImage(rostos, new Rect(0, 0, rostos.PixelWidth, rostos.PixelHeight));
            dc.DrawImage(gestos, new Rect(0, rostos.PixelHeight, gestos.PixelWidth, gestos.PixelHeight));
        }
        return Renderizar(visual, largura, altura);
    }

    // Ícones como o menu mostra: 14 caras e 13 itens a 96, 192 e 288 DPI, fundo claro e escuro.
    private static BitmapSource IconesDoMenuNativo()
    {
        const int margem = 12, folga = 6, titulo = 20;
        int[] dpis = [96, 192, 288];
        List<Tela> rostos = [.. Rostos.DeHumor.Select(IconesDoMenu.Rosto)];
        List<Tela> itens = [.. ItensPixel.Todos.Select(IconesDoMenu.Item)];
        int LarguraDaLinha(IEnumerable<Tela> telas, int fator) => telas.Sum(t => t.Largura * fator + folga);
        int metade = margem + dpis.Max(d => Math.Max(LarguraDaLinha(rostos, IconesDoMenu.Fator(d)), LarguraDaLinha(itens, IconesDoMenu.Fator(d)))) + margem;
        int altura = margem + dpis.Sum(d => titulo + (IconesDoMenu.AlturaDoRosto + IconesDoMenu.LadoDoItem) * IconesDoMenu.Fator(d) + 3 * folga);
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            foreach ((int x0, uint fundo) in new[] { (0, FundoDoMenu), (metade, FundoDoMenuEscuro) })
            {
                dc.DrawRectangle(new SolidColorBrush(ParaCor(fundo)), null, new Rect(x0, 0, metade, altura));
                int y = margem;
                foreach (int dpi in dpis)
                {
                    int fator = IconesDoMenu.Fator(dpi);
                    Texto(dc, $"{dpi} DPI ({fator}×)", x0 + margem, y, 12, true, (fundo & 0xFFFFFF) < 0x808080);
                    y += titulo;
                    foreach (List<Tela> linha in new[] { rostos, itens })
                    {
                        int x = x0 + margem;
                        foreach (Tela t in linha)
                        {
                            BitmapSource b = DoMenu(t, fator);
                            dc.DrawImage(b, new Rect(x, y, b.PixelWidth, b.PixelHeight));
                            x += b.PixelWidth + folga;
                        }
                        y += linha[0].Altura * fator + folga;
                    }
                    y += folga;
                }
            }
        }
        return Renderizar(visual, metade * 2, altura);
    }

    // Mesmos pixels do DIB do menu, BGRA com alfa.
    private static BitmapSource DoMenu(Tela t, int fator)
    {
        uint[] px = IconesDoMenu.Ampliar(t, fator);
        return BitmapSource.Create(t.Largura * fator, t.Altura * fator, 96, 96, PixelFormats.Bgra32, null, px, t.Largura * fator * 4);
    }

    // Itens ao lado do parado, mesma base. Em cima 2× (1 px de arte = 2 DIP), embaixo 1×.
    private static BitmapSource ItensAoLado(PosePixel parado)
    {
        Tela boneco = BonecoPixel.Desenhar(parado);
        List<Tela> itens = [.. ItensPixel.Todos.Select(ItensPixel.Desenhar)];
        const int margem = 12, folga = 6;
        int largura2 = 64 * 2 + itens.Count * (24 * 2 + folga);
        int metade = margem + largura2 + margem;
        int w = metade * 2, h = margem + 64 * 2 + margem + 64 + margem;
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(ParaCor(Fundo)), null, new Rect(0, 0, metade, h));
            dc.DrawRectangle(new SolidColorBrush(ParaCor(FundoEscuro)), null, new Rect(metade, 0, metade, h));
            foreach ((int x0, uint fundo) in new[] { (0, Fundo), (metade, FundoEscuro) })
            {
                foreach ((int escala, int baseY) in new[] { (2, margem + 64 * 2), (1, margem + 64 * 2 + margem + 64) })
                {
                    int x = x0 + margem;
                    BitmapSource b = Ampliada(boneco, escala, fundo);
                    dc.DrawImage(b, new Rect(x, baseY - b.PixelHeight, b.PixelWidth, b.PixelHeight));
                    x += b.PixelWidth;
                    foreach (Tela item in itens)
                    {
                        BitmapSource i = Ampliada(item, escala, fundo);
                        dc.DrawImage(i, new Rect(x, baseY - i.PixelHeight, i.PixelWidth, i.PixelHeight));
                        x += i.PixelWidth + folga;
                    }
                }
            }
        }
        return Renderizar(visual, w, h);
    }

    private static BitmapSource Ampliada(Tela t, int escala, uint fundo)
    {
        int w = t.Largura * escala, h = t.Altura * escala;
        var px = new uint[w * h];
        uint[] origem = t.ParaArgb();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                uint c = origem[y / escala * t.Largura + x / escala];
                px[y * w + x] = (c >> 24) == 0 ? fundo : c;
            }
        return BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
    }

    private static BitmapSource Grade(IReadOnlyList<(string Nome, Tela Tela)> itens, int escala, int colunas, uint fundo, bool rotulos = true, int alturaDoRotulo = 18)
    {
        int cw = itens.Max(i => i.Tela.Largura) * escala, ch = itens.Max(i => i.Tela.Altura) * escala;
        int margem = rotulos ? 10 : 0, rotulo = rotulos ? alturaDoRotulo : 0;
        int linhas = (itens.Count + colunas - 1) / colunas;
        int w = colunas * (cw + margem) + margem, h = linhas * (ch + margem + rotulo) + margem;
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(ParaCor(fundo)), null, new Rect(0, 0, w, h));
            for (int i = 0; i < itens.Count; i++)
            {
                int x = margem + i % colunas * (cw + margem), y = margem + i / colunas * (ch + margem + rotulo);
                BitmapSource img = Ampliada(itens[i].Tela, escala, fundo);
                dc.DrawImage(img, new Rect(x, y, img.PixelWidth, img.PixelHeight));
                if (rotulos)
                {
                    var texto = new FormattedText(itens[i].Nome, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"), 12, new SolidColorBrush((fundo & 0xFFFFFF) < 0x808080 ? Colors.Gainsboro : Color.FromRgb(0x33, 0x33, 0x40)), 1.0);
                    dc.DrawText(texto, new Point(x, y + ch + 2));
                }
            }
        }
        return Renderizar(visual, w, h);
    }

    private static void Texto(DrawingContext dc, string texto, double x, double y, double tamanho, bool negrito, bool claro = false)
    {
        var ft = new FormattedText(texto, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, negrito ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
            tamanho, new SolidColorBrush(claro ? Colors.Gainsboro : Color.FromRgb(0x33, 0x33, 0x40)), 1.0);
        dc.DrawText(ft, new Point(x, y));
    }

    private static BitmapSource Renderizar(DrawingVisual visual, int w, int h)
    {
        var alvo = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        alvo.Render(visual);
        return alvo;
    }

    private static Color ParaCor(uint argb) => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    private static void Salvar(BitmapSource bmp, string caminho)
    {
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bmp));
        using FileStream f = File.Create(caminho);
        png.Save(f);
        Console.WriteLine($"  {caminho}");
    }
}
