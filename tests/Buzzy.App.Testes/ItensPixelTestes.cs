using Buzzy.Testes;
using Buzzy.Visual.Pixel;

namespace Buzzy.App.Testes;

/// <summary>
/// Itens do tamagotchi em pixel art (DEC-028, passo A1): os 13 itens na ordem do menu e com as
/// chaves do enum <c>Item</c> do núcleo em minúsculas, o verbo de cada um, o desenho do chão em
/// 24 × 24 (pousa na última linha como os pés, com folga no topo e nas laterais, alfa só 0 ou 255
/// e contorno fechado) e o carimbo pequeno "na mão", com pega, boca e variantes.
/// </summary>
internal sealed class ItensPixelTestes
{
    /// <summary>Ordem da resposta do usuário e do menu; a chave é o nome do enum em minúsculas.</summary>
    private static readonly string[] Ordem =
        ["banana", "agua", "vodka", "cerveja", "baseado", "cigarro", "cocaina", "md", "lancaperfume", "cafe", "energetico", "cogumelo", "bala"];

    private static int Contar(Tela t, Cor cor)
    {
        int n = 0;
        for (int y = 0; y < t.Altura; y++)
            for (int x = 0; x < t.Largura; x++)
                if (t[x, y] == cor) n++;
        return n;
    }

    private static int Contar(Carimbo c, Cor cor)
    {
        int n = 0;
        for (int y = 0; y < c.Altura; y++)
            for (int x = 0; x < c.Largura; x++)
                if (c[x, y] == cor) n++;
        return n;
    }

    [Teste]
    public void OsTrezeItensNaOrdemDoMenuComAsChavesDoNucleo()
    {
        Afirmar.Sequencia(Ordem, ItensPixel.Todos);
    }

    [Teste]
    public void CadaItemTemOVerboDaTabela()
    {
        var esperado = new Dictionary<string, Verbo>
        {
            ["banana"] = Verbo.Comer,
            ["cogumelo"] = Verbo.Comer,
            ["agua"] = Verbo.Beber,
            ["vodka"] = Verbo.Beber,
            ["cerveja"] = Verbo.Beber,
            ["cafe"] = Verbo.Beber,
            ["energetico"] = Verbo.Beber,
            ["baseado"] = Verbo.Fumar,
            ["cigarro"] = Verbo.Fumar,
            ["cocaina"] = Verbo.Cheirar,
            ["md"] = Verbo.Engolir,
            ["bala"] = Verbo.Engolir,
            ["lancaperfume"] = Verbo.Inalar,
        };
        Afirmar.Igual(Ordem.Length, esperado.Count);
        foreach (string item in Ordem) Afirmar.Igual(esperado[item], ItensPixel.VerboDe(item), item);
    }

    [Teste]
    public void ChaveDesconhecidaLancaComMensagemClara()
    {
        // "lanca" era a chave do desenho da arte; a do núcleo é "lancaperfume" (crítica, F6).
        foreach (string chave in new[] { "lanca", "Banana", "", "maconha" })
        {
            Afirmar.Lanca<ArgumentException>(() => ItensPixel.Desenhar(chave), $"desenho de '{chave}'");
            Afirmar.Lanca<ArgumentException>(() => ItensPixel.VerboDe(chave), $"verbo de '{chave}'");
            Afirmar.Lanca<ArgumentException>(() => ItensPixel.NaMao(chave), $"mão de '{chave}'");
        }
        Afirmar.Lanca<ArgumentException>(() => ItensPixel.NaMao("banana", "assada"), "variante desconhecida");
    }

    [Teste]
    public void CadaItemTem24x24EAlfaBinario()
    {
        foreach (string item in Ordem)
        {
            Tela t = ItensPixel.Desenhar(item);
            Afirmar.Igual((ItensPixel.Lado, ItensPixel.Lado), (t.Largura, t.Altura), item);
            uint[] argb = t.ParaArgb();
            Afirmar.Igual(0, argb.Count(p => (p >> 24) is not 0 and not 255), $"{item}: alfa só 0 ou 255");
            Afirmar.Verdadeiro(argb.Count(p => p >> 24 == 255) >= 40, $"{item}: desenho de verdade, não um ponto");
        }
    }

    [Teste]
    public void PousaNaUltimaLinhaCentradoComFolgaNoTopoENasLaterais()
    {
        foreach (string item in Ordem)
        {
            (int e, int t, int d, int b) = Afirmar.NaoNulo(ItensPixel.Desenhar(item).Limites(), item);
            Afirmar.Igual(ItensPixel.Lado, b, $"{item}: o contorno de baixo fica na última linha, como os pés");
            Afirmar.Verdadeiro(e >= 1 && t >= 1 && d <= ItensPixel.Lado - 1, $"{item}: 1 px livre no topo e nas laterais ({e},{t})-({d},{b})");
            Afirmar.Verdadeiro(Math.Abs(e + d - ItensPixel.Lado) <= 1, $"{item}: centrado na horizontal ({e}..{d})");
        }
    }

    [Teste]
    public void TodoItemEhFacilDeAgarrar()
    {
        // Só os pixels opacos recebem o clique (crítica, L14): a 100%, cada pixel de arte tem 2 DIP, e um
        // item com menos de 8 pixels de altura (16 DIP) fica difícil de agarrar.
        foreach (string item in Ordem)
        {
            (int e, int t, int d, int b) = Afirmar.NaoNulo(ItensPixel.Desenhar(item).Limites(), item);
            Afirmar.Verdadeiro(b - t >= 8 && d - e >= 8, $"{item}: {d - e}×{b - t} pixels de arte, menos de 8 de lado");
        }
    }

    [Teste]
    public void ContornoFechaASilhueta()
    {
        foreach (string item in Ordem)
        {
            Tela t = ItensPixel.Desenhar(item);
            for (int y = 0; y < t.Altura; y++)
            {
                for (int x = 0; x < t.Largura; x++)
                {
                    if (!t.Opaco(x, y)) continue;
                    bool borda = !t.Opaco(x - 1, y) || !t.Opaco(x + 1, y) || !t.Opaco(x, y - 1) || !t.Opaco(x, y + 1);
                    if (borda) Afirmar.Igual(Cor.Contorno, t[x, y], $"{item}: pixel ({x},{y}) na borda da silhueta sem contorno");
                }
            }
        }
    }

    [Teste]
    public void CadaItemTemSuaCorAssinatura()
    {
        var assinatura = new Dictionary<string, Cor[]>
        {
            ["banana"] = [Cor.Banana],
            ["agua"] = [Cor.Agua],
            ["vodka"] = [Cor.Vidro, Cor.Faixa],
            ["cerveja"] = [Cor.Cerveja, Cor.Branco],
            ["baseado"] = [Cor.Papel, Cor.Cipo],
            ["cigarro"] = [Cor.Filtro, Cor.Papel],
            ["cocaina"] = [Cor.Estrela, Cor.AguaEscura, Cor.Branco],
            ["md"] = [Cor.Lilas],
            ["lancaperfume"] = [Cor.Vidro, Cor.Metal],
            ["cafe"] = [Cor.Cafe, Cor.Branco],
            ["energetico"] = [Cor.Neon, Cor.Metal],
            ["cogumelo"] = [Cor.Branco, Cor.Creme],
            ["bala"] = [Cor.Rosa],
        };
        foreach (string item in Ordem)
        {
            Tela t = ItensPixel.Desenhar(item);
            foreach (Cor cor in assinatura[item])
                Afirmar.Verdadeiro(Contar(t, cor) >= 3, $"{item}: tem {cor}");
        }
    }

    [Teste]
    public void NenhumItemTemAsCoresDoPelo()
    {
        // Revisão da arte, achado 12: a lata do energético era pintada com as cores do pelo — sumia na mão,
        // contra o corpo, e na boca parecia uma barba. Nenhum item, no chão ou na mão, usa o azul do pelo.
        Cor[] pelo = [Cor.Pelo, Cor.PeloEscuro, Cor.PeloClaro];
        foreach (string item in Ordem)
        {
            Tela chao = ItensPixel.Desenhar(item);
            foreach (Cor cor in pelo) Afirmar.Igual(0, Contar(chao, cor), $"{item}: {cor} no chão");
            foreach (string variante in ItensPixel.VariantesNaMao(item))
                foreach (Cor cor in pelo) Afirmar.Igual(0, Contar(ItensPixel.NaMao(item, variante).Desenho, cor), $"{item}/{variante}: {cor} na mão");
        }
    }

    [Teste]
    public void SilhuetasDistintas()
    {
        // Índice de Jaccard das máscaras opacas: dois itens com quase a mesma forma confundiriam.
        bool[][] mascaras = [.. Ordem.Select(i => ItensPixel.Desenhar(i).ParaArgb().Select(p => p >> 24 == 255).ToArray())];
        for (int a = 0; a < Ordem.Length; a++)
        {
            for (int b = a + 1; b < Ordem.Length; b++)
            {
                int inter = 0, uniao = 0;
                for (int i = 0; i < mascaras[a].Length; i++)
                {
                    if (mascaras[a][i] && mascaras[b][i]) inter++;
                    if (mascaras[a][i] || mascaras[b][i]) uniao++;
                }
                Afirmar.Verdadeiro(inter / (double)uniao < 0.8, $"{Ordem[a]} e {Ordem[b]} quase iguais ({inter}/{uniao})");
            }
        }
    }

    [Teste]
    public void PontosDeTesteBatemComOsPixels()
    {
        foreach (string item in Ordem)
        {
            Tela t = ItensPixel.Desenhar(item);
            ((int X, int Y) opaco, (int X, int Y) transparente) = ItensPixel.PontosDeTeste(item);
            Afirmar.Verdadeiro(t.Opaco(opaco.X, opaco.Y), $"{item}: ponto opaco {opaco}");
            Afirmar.Verdadeiro(t.Dentro(transparente.X, transparente.Y) && !t.Opaco(transparente.X, transparente.Y), $"{item}: ponto transparente {transparente}");
            // O ponto opaco fica no corpo do item, não no contorno: o clique pega o item com folga.
            Afirmar.Diferente(Cor.Contorno, t[opaco.X, opaco.Y], $"{item}: ponto opaco no corpo");
        }
    }

    /// <summary>As variantes na mão por verbo (passo A3): os itens do mesmo verbo têm as mesmas.</summary>
    private static readonly Dictionary<Verbo, string[]> VariantesDoVerbo = new()
    {
        [Verbo.Comer] = ["aberto", "mordido", "resto"],
        [Verbo.Beber] = ["normal", "gole"],
        [Verbo.Fumar] = ["aceso", "tragando"],
        [Verbo.Cheirar] = ["cheia", "meia", "vazia"],
        [Verbo.Engolir] = ["normal", "na-boca"],
        [Verbo.Inalar] = ["frasco", "lenco"],
    };

    [Teste]
    public void VariantesNaMaoDaTabela()
    {
        foreach (string item in Ordem)
        {
            string[] variantes = VariantesDoVerbo[ItensPixel.VerboDe(item)];
            Afirmar.Sequencia(variantes, ItensPixel.VariantesNaMao(item), item);
            ItemNaMao padrao = ItensPixel.NaMao(item), primeira = ItensPixel.NaMao(item, variantes[0]);
            Afirmar.Igual((padrao.Pega, padrao.Ponta), (primeira.Pega, primeira.Ponta), $"{item}: sem variante, vale a primeira");
        }
    }

    [Teste]
    public void SoAsVariantesQueVaoAoRostoTemPonta()
    {
        // À vista (a bebida em pé, o cigarro aceso, o espelho cheio na palma), o resto da comida e o frasco
        // não vão ao rosto.
        string[] semPonta = ["normal", "aceso", "resto", "frasco", "cheia"];
        foreach (string item in Ordem)
        {
            foreach (string variante in ItensPixel.VariantesNaMao(item))
            {
                bool vaiAoRosto = !semPonta.Contains(variante) || ItensPixel.VerboDe(item) == Verbo.Engolir;
                Afirmar.Igual(vaiAoRosto, ItensPixel.NaMao(item, variante).Ponta is not null, $"{item}/{variante}");
            }
        }
    }

    [Teste]
    public void NaMaoCabePertoDaMaoComPegaEPontaNoDesenho()
    {
        foreach (string item in Ordem)
        {
            foreach (string variante in ItensPixel.VariantesNaMao(item))
            {
                ItemNaMao m = ItensPixel.NaMao(item, variante);
                string onde = $"{item}/{variante}";
                Afirmar.Verdadeiro(m.Desenho.Largura <= 14 && m.Desenho.Altura <= 14, $"{onde}: cabe perto da mão ({m.Desenho.Largura}×{m.Desenho.Altura})");
                if (ItensPixel.VerboDe(item) == Verbo.Engolir || (item, variante) == ("cocaina", "cheia"))
                {
                    // Na ponta dos dedos (o comprimido, a bala) ou na palma, como uma bandeja (o espelho à
                    // vista): a mão fica logo abaixo, sem cobrir o item.
                    Afirmar.Verdadeiro(m.Pega.Y >= m.Desenho.Altura + 1 && m.Pega.Y <= m.Desenho.Altura + 4, $"{onde}: a pega {m.Pega} fica logo abaixo do desenho");
                    Afirmar.Verdadeiro(m.Pega.X >= 0 && m.Pega.X < m.Desenho.Largura, $"{onde}: a pega fica embaixo do item");
                }
                else
                {
                    Afirmar.Verdadeiro(m.Desenho[m.Pega.X, m.Pega.Y] is not null, $"{onde}: a pega {m.Pega} fica no desenho");
                }
                if (m.Ponta is { } ponta) Afirmar.Verdadeiro(m.Desenho[ponta.X, ponta.Y] is not null, $"{onde}: a ponta {ponta} fica no desenho");
            }
        }
    }

    [Teste]
    public void NoGoleENaTragadaAPontaFicaNaBordaVoltadaParaABoca()
    {
        // A garrafa, a lata e o fumo deitam com a ponta na coluna da esquerda, rumo à boca; a caneca
        // e a xícara ficam em pé, com a ponta na borda de cima.
        foreach ((string item, string variante, bool deitado) in new[]
        {
            ("agua", "gole", true), ("vodka", "gole", true), ("energetico", "gole", true), ("cerveja", "gole", false), ("cafe", "gole", false),
            ("baseado", "tragando", true), ("cigarro", "tragando", true), ("banana", "aberto", false), ("cogumelo", "aberto", false),
        })
        {
            ItemNaMao m = ItensPixel.NaMao(item, variante);
            (int X, int Y) ponta = Afirmar.NaoNulo(m.Ponta, $"{item}/{variante}");
            if (deitado) Afirmar.Igual(0, ponta.X, $"{item}/{variante}: a ponta fica na coluna da esquerda");
            else Afirmar.Igual(0, ponta.Y, $"{item}/{variante}: a ponta fica na linha de cima");
            if (deitado) Afirmar.Verdadeiro(m.Desenho.Largura > m.Desenho.Altura, $"{item}/{variante}: deitado");
        }
    }

    [Teste]
    public void GiradoLevaAPontaDeCimaParaAEsquerda()
    {
        foreach (string item in Ordem)
        {
            foreach (string variante in ItensPixel.VariantesNaMao(item))
            {
                ItemNaMao m = ItensPixel.NaMao(item, variante), g = m.Girado();
                string onde = $"{item}/{variante}";
                int largura = m.Desenho.Largura;
                Afirmar.Igual((m.Desenho.Altura, largura), (g.Desenho.Largura, g.Desenho.Altura), $"{onde}: troca largura e altura");
                Afirmar.Igual((m.Pega.Y, largura - 1 - m.Pega.X), g.Pega, $"{onde}: pega girada");
                Carimbo esperado = m.Desenho.Girado(horario: false);
                for (int y = 0; y < esperado.Altura; y++)
                    for (int x = 0; x < esperado.Largura; x++)
                        Afirmar.Igual(esperado[x, y], g.Desenho[x, y], $"{onde}: pixel ({x},{y})");
                if (m.Ponta is { } ponta)
                {
                    (int X, int Y) p = Afirmar.NaoNulo(g.Ponta, onde);
                    Afirmar.Igual((ponta.Y, largura - 1 - ponta.X), p, $"{onde}: ponta girada");
                    if (ponta.Y == 0) Afirmar.Igual(0, p.X, $"{onde}: girado, a ponta de cima vai para a coluna da esquerda");
                }
                else
                {
                    Afirmar.Nulo(g.Ponta, onde);
                }
            }
        }
    }

    [Teste]
    public void TragarAcendeABrasa()
    {
        foreach (string item in new[] { "baseado", "cigarro" })
        {
            Carimbo aceso = ItensPixel.NaMao(item, "aceso").Desenho, tragando = ItensPixel.NaMao(item, "tragando").Desenho;
            Afirmar.Verdadeiro(Contar(tragando, Cor.BrasaClara) > Contar(aceso, Cor.BrasaClara), $"{item}: a tragada acende a brasa");
            Afirmar.Verdadeiro(Contar(aceso, Cor.Brasa) > 0, $"{item}: aceso, com a brasa à vista");
            // Na mão, aceso, a brasa fica para cima; na tragada, deitado, ela vai para a direita, longe da boca.
            Afirmar.Verdadeiro(Enumerable.Range(0, aceso.Largura).Any(x => aceso[x, 0] == Cor.Brasa), $"{item}: aceso com a brasa em cima");
            Afirmar.Verdadeiro(Enumerable.Range(0, tragando.Altura).Any(y => tragando[tragando.Largura - 1, y] == Cor.BrasaClara), $"{item}: na tragada, a brasa na ponta da direita");
        }
    }

    [Teste]
    public void EspelhoEsvaziaAoCheirar()
    {
        int cheia = Contar(ItensPixel.NaMao("cocaina", "cheia").Desenho, Cor.Branco);
        int meia = Contar(ItensPixel.NaMao("cocaina", "meia").Desenho, Cor.Branco);
        int vazia = Contar(ItensPixel.NaMao("cocaina", "vazia").Desenho, Cor.Branco);
        Afirmar.Verdadeiro(cheia > meia && meia > vazia && vazia == 0, $"cheia {cheia} > meia {meia} > vazia {vazia} = 0");
    }

    [Teste]
    public void ComerAcabaComAComida()
    {
        // A polpa da banana e o chapéu do cogumelo diminuem a cada mordida; no fim, só a casca e o pezinho.
        foreach ((string item, Cor[] comida) in new[] { ("banana", new[] { Cor.Branco, Cor.CremeClaro, Cor.Creme }), ("cogumelo", new[] { Cor.Coracao, Cor.Faixa }) })
        {
            int Comida(string variante) => comida.Sum(c => Contar(ItensPixel.NaMao(item, variante).Desenho, c));
            int aberto = Comida("aberto"), mordido = Comida("mordido"), resto = Comida("resto");
            Afirmar.Verdadeiro(aberto > mordido && mordido > resto && resto == 0, $"{item}: aberto {aberto} > mordido {mordido} > resto {resto} = 0");
        }
        Afirmar.Verdadeiro(Contar(ItensPixel.NaMao("banana", "resto").Desenho, Cor.Banana) > 0, "sobra a casca amarela");
    }

    [Teste]
    public void DesenharDevolveTelaNova()
    {
        Tela a = ItensPixel.Desenhar("banana");
        a[12, 12] = Cor.Faixa;
        Afirmar.Diferente(Cor.Faixa, ItensPixel.Desenhar("banana")[12, 12], "mexer na tela devolvida não muda o item");
    }
}
