using Buzzy.Testes;

namespace Buzzy.Core.Testes;

/// <summary>
/// Testes de propriedade com topologias e posições aleatórias (<see cref="GeradorDeTopologias"/>).
/// A semente é fixa, então cada execução gera os mesmos casos. Cada caso tem a própria
/// semente, tirada da semente mestra; toda falha informa a semente mestra, o número do caso,
/// a semente do caso e os dados de entrada, para o caso virar um teste de exemplo.
/// </summary>
internal static class PropriedadesTestes
{
    private const int Semente = 20260929;
    private const int Casos = 5000;

    [Teste]
    public static void GeradorProduzTopologiasValidas()
    {
        ParaCadaCaso((_, gerador, contexto) =>
        {
            Topologia t = gerador.Mudar(gerador.NovaTopologia());
            string Onde() => $"{contexto()}; topologia: {t}";

            Verificar(t.Monitores.Count is >= 1 and <= GeradorDeTopologias.MaximoDeMonitores, () => $"{Onde()}: {t.Monitores.Count} monitores");
            Verificar(t.Principal.Tela.Esquerda == 0 && t.Principal.Tela.Topo == 0, () => $"{Onde()}: principal fora da origem");
            foreach (MonitorDoDesktop m in t.Monitores)
                Verificar(m.Dpi is >= 96 and <= 288, () => $"{Onde()}: DPI {m.Dpi}");
            for (int i = 0; i < t.Monitores.Count; i++)
                for (int j = i + 1; j < t.Monitores.Count; j++)
                    Verificar(!t.Monitores[i].Tela.Intersecta(t.Monitores[j].Tela), () => $"{Onde()}: monitores sobrepostos");
        });
    }

    [Teste]
    public static void NoMonitorSempreDevolveUmPosicionamentoValido()
    {
        ParaCadaCaso((rnd, gerador, contexto) =>
        {
            Topologia t = gerador.NovaTopologia();
            MonitorDoDesktop m = t.Monitores[rnd.Next(t.Monitores.Count)];
            double fx = gerador.Fracao(), fy = gerador.Fracao();
            TamanhoDip tamanho = gerador.Sprite();
            string Onde() => $"{contexto()}; topologia: {t}; monitor {m.Chave}; frações ({fx}; {fy}); sprite {tamanho}";

            Posicionamento p = Posicionador.NoMonitor(m, fx, fy, tamanho);
            Verificar(ReferenceEquals(m, p.Monitor), () => $"{Onde()}: devolveu outro monitor, {p.Monitor.Chave}");
            VerificarPosicionamento(p, tamanho, Onde);

            // Determinístico: um monitor igual, em outra instância, dá o mesmo resultado.
            Verificar(p == Posicionador.NoMonitor(m with { }, fx, fy, tamanho), () => $"{Onde()}: resultado mudou na segunda chamada");

            // Descrever e voltar reproduz o posicionamento.
            PosicaoDoPersonagem d = Posicionador.Descrever(p);
            Verificar(p == Posicionador.NoMonitor(m, d.FracaoX, d.FracaoY, tamanho), () => $"{Onde()}: Descrever e NoMonitor não voltam ao mesmo lugar ({d})");

            // Frações nos extremos encostam o sprite na borda correspondente, quando ele cabe.
            RetanguloPx area = m.AreaUtil;
            if (p.Tamanho.Largura <= area.Largura)
            {
                if (fx <= 0) Verificar(p.Retangulo.Esquerda == area.Esquerda, () => $"{Onde()}: fração x {fx} não encostou na esquerda: {p.Retangulo}");
                if (fx >= 1) Verificar(p.Retangulo.Direita == area.Direita, () => $"{Onde()}: fração x {fx} não encostou na direita: {p.Retangulo}");
            }
            if (p.Tamanho.Altura <= area.Altura)
            {
                if (fy <= 0) Verificar(p.Retangulo.Topo == area.Topo, () => $"{Onde()}: fração y {fy} não encostou no topo: {p.Retangulo}");
                if (fy >= 1) Verificar(p.Retangulo.Base == area.Base, () => $"{Onde()}: fração y {fy} não pôs os pés no chão: {p.Retangulo}");
            }

            // Fração maior nunca põe a âncora mais para a esquerda nem mais para cima.
            if (!double.IsNaN(fx) && !double.IsNaN(fy))
            {
                double fx2 = fx + rnd.NextDouble() * 0.3, fy2 = fy + rnd.NextDouble() * 0.3;
                Posicionamento p2 = Posicionador.NoMonitor(m, fx2, fy2, tamanho);
                Verificar(p2.Ancora.X >= p.Ancora.X && p2.Ancora.Y >= p.Ancora.Y,
                    () => $"{Onde()}: frações ({fx2}; {fy2}) deram âncora {p2.Ancora}, antes de {p.Ancora}");
            }
        });
    }

    [Teste]
    public static void ReacomodarSempreTerminaNaAreaUtilDeUmMonitorDaNovaTopologia()
    {
        ParaCadaCaso((rnd, gerador, contexto) =>
        {
            Topologia antiga = gerador.NovaTopologia();
            Topologia nova = gerador.Mudar(antiga);
            TamanhoDip tamanho = gerador.Sprite();

            // Quase sempre uma posição real na topologia antiga; às vezes uma posição qualquer,
            // com chave que pode não existir e última âncora em qualquer lugar.
            PosicaoDoPersonagem atual = rnd.Next(5) != 0
                ? Posicionador.Descrever(Posicionador.NoMonitor(antiga.Monitores[rnd.Next(antiga.Monitores.Count)], gerador.Fracao(), gerador.Fracao(), tamanho))
                : new PosicaoDoPersonagem(GeradorDeTopologias.Chave(rnd.Next(1, 12)), gerador.Fracao(), gerador.Fracao(), gerador.Ponto(antiga));
            string Onde() => $"{contexto()}; antiga: {antiga}; nova: {nova}; posição: {atual}; sprite {tamanho}";

            (Posicionamento r, PosicaoDoPersonagem posicao) = Posicionador.Reacomodar(nova, atual, tamanho);

            Verificar(nova.Monitores.Any(m => ReferenceEquals(m, r.Monitor)), () => $"{Onde()}: monitor {r.Monitor.Chave} não pertence à nova topologia");
            VerificarPosicionamento(r, tamanho, Onde);
            Verificar(posicao.ChaveMonitor == r.Monitor.Chave, () => $"{Onde()}: nova posição aponta para {posicao.ChaveMonitor}, resultado está em {r.Monitor.Chave}");
            Verificar(posicao.AncoraAbsoluta == r.Ancora, () => $"{Onde()}: âncora absoluta {posicao.AncoraAbsoluta} diferente da âncora {r.Ancora}");
            Verificar(posicao.TelaDoMonitor == r.Monitor.Tela, () => $"{Onde()}: a nova posição guarda a tela {posicao.TelaDoMonitor}, a do monitor é {r.Monitor.Tela}");

            if (nova.PorChave(atual.ChaveMonitor) is not null)
            {
                // O monitor continua: fica nele, com as mesmas frações (campo a campo: a âncora e a
                // tela guardada passam a ser as de agora, conferidas acima; Equals porque NaN fica NaN).
                Verificar(r.Monitor.Chave == atual.ChaveMonitor, () => $"{Onde()}: saiu do monitor {atual.ChaveMonitor}, que continua na topologia");
                Verificar(posicao.FracaoX.Equals(atual.FracaoX) && posicao.FracaoY.Equals(atual.FracaoY), () => $"{Onde()}: frações mudaram: {posicao}");
            }
            else
            {
                // O monitor sumiu: vai para o de tela mais próxima do pixel dos pés da última âncora
                // (Posicionador.PixelDosPes); em empate, o principal.
                PontoPx pes = Posicionador.PixelDosPes(atual.AncoraAbsoluta);
                long menor = nova.Monitores.Min(m => m.Tela.DistanciaAoQuadrado(pes));
                long obtida = r.Monitor.Tela.DistanciaAoQuadrado(pes);
                Verificar(obtida == menor, () => $"{Onde()}: {r.Monitor.Chave} está a d² = {obtida} do pixel dos pés da última âncora, e há monitor a d² = {menor}");
                bool principalEmpata = nova.Principal.Tela.DistanciaAoQuadrado(pes) == menor;
                Verificar(!principalEmpata || r.Monitor.Principal, () => $"{Onde()}: empate com o principal, mas escolheu {r.Monitor.Chave}");
            }

            // Determinístico: cópias iguais das entradas dão o mesmo resultado.
            var copia = new Topologia(nova.Monitores.Select(m => m with { }));
            Verificar((r, posicao) == Posicionador.Reacomodar(copia, atual with { }, tamanho), () => $"{Onde()}: resultado mudou na segunda chamada");

            // Reaplicar na mesma topologia não move o personagem.
            Verificar((r, posicao) == Posicionador.Reacomodar(nova, posicao, tamanho), () => $"{Onde()}: reacomodar de novo moveu o personagem");

            // Se a topologia antiga voltar, o personagem não pula de volta: fica no monitor da
            // nova posição sempre que ele existe lá.
            if (antiga.PorChave(posicao.ChaveMonitor) is not null)
            {
                string volta = Posicionador.Reacomodar(antiga, posicao, tamanho).Resultado.Monitor.Chave;
                Verificar(volta == posicao.ChaveMonitor, () => $"{Onde()}: com a topologia antiga de volta, pulou de {posicao.ChaveMonitor} para {volta}");
            }
        });
    }

    [Teste]
    public static void PrenderNaAreaUtilDevolveAAncoraValidaMaisProxima()
    {
        ParaCadaCaso((rnd, _, contexto) =>
        {
            var area = RetanguloPx.DePosicaoETamanho(new PontoPx(rnd.Next(-5000, 5001), rnd.Next(-5000, 5001)), new TamanhoPx(rnd.Next(1, 4001), rnd.Next(1, 4001)));
            var tamanho = new TamanhoPx(rnd.Next(1, 3001), rnd.Next(1, 3001));
            var ancora = new PontoPx(rnd.Next(area.Esquerda - 6000, area.Direita + 6000), rnd.Next(area.Topo - 6000, area.Base + 6000));
            string Onde() => $"{contexto()}; área {area}; sprite {tamanho}; âncora {ancora}";

            PontoPx a = Posicionador.PrenderNaAreaUtil(ancora, tamanho, area);
            RetanguloPx r = Posicionador.RetanguloDoSprite(a, tamanho);
            bool cabeX = tamanho.Largura <= area.Largura, cabeY = tamanho.Altura <= area.Altura;

            if (cabeX)
            {
                Verificar(r.Esquerda >= area.Esquerda && r.Direita <= area.Direita, () => $"{Onde()}: {r} sai da área na horizontal");
                // Mais próxima: só sai do lugar pedido até encostar na borda.
                if (ancora.X < a.X) Verificar(r.Esquerda == area.Esquerda, () => $"{Onde()}: moveu a âncora para {a} sem encostar na borda esquerda");
                if (ancora.X > a.X) Verificar(r.Direita == area.Direita, () => $"{Onde()}: moveu a âncora para {a} sem encostar na borda direita");
            }
            else
            {
                int sobraEsquerda = area.Esquerda - r.Esquerda, sobraDireita = r.Direita - area.Direita;
                Verificar(sobraEsquerda >= 0 && sobraDireita >= 0 && Math.Abs(sobraEsquerda - sobraDireita) <= 1,
                    () => $"{Onde()}: sprite largo não ficou centralizado: {r}");
            }

            if (cabeY)
            {
                Verificar(r.Topo >= area.Topo && r.Base <= area.Base, () => $"{Onde()}: {r} sai da área na vertical");
                if (ancora.Y < a.Y) Verificar(r.Topo == area.Topo, () => $"{Onde()}: moveu a âncora para {a} sem encostar no topo");
                if (ancora.Y > a.Y) Verificar(r.Base == area.Base, () => $"{Onde()}: moveu a âncora para {a} sem encostar no chão");
            }
            else
            {
                Verificar(r.Base == area.Base, () => $"{Onde()}: sprite alto não ficou com os pés no chão: {r}");
            }

            if (cabeX && cabeY && area.Contem(Posicionador.RetanguloDoSprite(ancora, tamanho)))
                Verificar(a == ancora, () => $"{Onde()}: âncora válida foi movida para {a}");

            Verificar(Posicionador.PrenderNaAreaUtil(a, tamanho, area) == a, () => $"{Onde()}: prender de novo moveu a âncora {a}");

            // Não depende de onde a área está: deslocar tudo desloca o resultado.
            int dx = rnd.Next(-3000, 3001), dy = rnd.Next(-3000, 3001);
            PontoPx deslocada = Posicionador.PrenderNaAreaUtil(new PontoPx(ancora.X + dx, ancora.Y + dy), tamanho, area.Deslocado(dx, dy));
            Verificar(deslocada == new PontoPx(a.X + dx, a.Y + dy), () => $"{Onde()}: deslocando ({dx},{dy}) deu {deslocada}, esperado {new PontoPx(a.X + dx, a.Y + dy)}");
        });
    }

    [Teste]
    public static void MonitorMaisProximoMinimizaADistanciaEPrefereOPrincipal()
    {
        ParaCadaCaso((_, gerador, contexto) =>
        {
            Topologia t = gerador.NovaTopologia();
            PontoPx p = gerador.Ponto(t);
            string Onde() => $"{contexto()}; topologia: {t}; ponto {p}";

            MonitorDoDesktop? contem = t.MonitorQueContem(p);
            int quantosContem = t.Monitores.Count(m => m.Tela.Contem(p));
            Verificar(contem is null ? quantosContem == 0 : contem.Tela.Contem(p) && quantosContem == 1,
                () => $"{Onde()}: MonitorQueContem devolveu {contem?.Chave ?? "nulo"}, {quantosContem} telas contêm o ponto");

            MonitorDoDesktop maisProximo = t.MonitorMaisProximo(p);
            Verificar(t.Monitores.Any(m => ReferenceEquals(m, maisProximo)), () => $"{Onde()}: monitor fora da topologia");
            if (contem is not null)
                Verificar(ReferenceEquals(contem, maisProximo), () => $"{Onde()}: o ponto está em {contem.Chave}, mas o mais próximo foi {maisProximo.Chave}");

            long menor = t.Monitores.Min(m => m.Tela.DistanciaAoQuadrado(p));
            Verificar(maisProximo.Tela.DistanciaAoQuadrado(p) == menor, () => $"{Onde()}: {maisProximo.Chave} não é o de menor distância (d² = {menor})");

            // Empate: vence o principal; sem o principal no empate, o primeiro da lista.
            MonitorDoDesktop esperado = t.Principal.Tela.DistanciaAoQuadrado(p) == menor
                ? t.Principal
                : t.Monitores.First(m => m.Tela.DistanciaAoQuadrado(p) == menor);
            Verificar(ReferenceEquals(esperado, maisProximo), () => $"{Onde()}: em empate esperava {esperado.Chave}, veio {maisProximo.Chave}");
        });
    }

    // ---------------------------------------------------------------- auxiliares

    /// <summary>
    /// Confere o que vale para todo posicionamento: tamanho físico pelo DPI do monitor,
    /// retângulo coerente com a âncora, sprite inteiro na área útil quando cabe (centralizado
    /// se for largo demais, com os pés no chão se for alto demais) e o pixel dos pés, logo
    /// acima da âncora, dentro da área útil.
    /// </summary>
    internal static void VerificarPosicionamento(Posicionamento p, TamanhoDip tamanho, Func<string> onde)
    {
        RetanguloPx area = p.Monitor.AreaUtil;
        RetanguloPx r = p.Retangulo;
        bool cabeX = p.Tamanho.Largura <= area.Largura, cabeY = p.Tamanho.Altura <= area.Altura;

        Verificar(p.Tamanho == tamanho.ParaPixels(p.Monitor.Dpi), () => $"{onde()}: tamanho {p.Tamanho} não corresponde a {p.Monitor.Dpi} DPI");
        Verificar(r == Posicionador.RetanguloDoSprite(p.Ancora, p.Tamanho), () => $"{onde()}: retângulo {r} não corresponde à âncora {p.Ancora}");

        if (cabeX && cabeY)
            Verificar(area.Contem(r), () => $"{onde()}: sprite cabe, mas {r} sai da área útil {area}");
        if (!cabeX)
        {
            int sobraEsquerda = area.Esquerda - r.Esquerda, sobraDireita = r.Direita - area.Direita;
            Verificar(sobraEsquerda >= 0 && sobraDireita >= 0 && Math.Abs(sobraEsquerda - sobraDireita) <= 1,
                () => $"{onde()}: sprite largo não ficou centralizado em {area}: {r}");
        }
        if (!cabeY)
            Verificar(r.Base == area.Base, () => $"{onde()}: sprite alto não ficou com os pés no chão de {area}: {r}");

        Verificar(area.Contem(new PontoPx(p.Ancora.X, p.Ancora.Y - 1)), () => $"{onde()}: âncora {p.Ancora} fora da área útil {area}");
    }

    /// <summary>Roda <see cref="Casos"/> casos, cada um com a própria semente tirada da semente mestra.</summary>
    private static void ParaCadaCaso(Action<Random, GeradorDeTopologias, Func<string>> verificar)
    {
        var mestre = new Random(Semente);
        for (int caso = 0; caso < Casos; caso++)
        {
            int sementeDoCaso = mestre.Next();
            var rnd = new Random(sementeDoCaso);
            int numero = caso;
            verificar(rnd, new GeradorDeTopologias(rnd), () => $"semente {Semente}, caso {numero} (semente do caso {sementeDoCaso})");
        }
    }

    private static void Verificar(bool condicao, Func<string> mensagem)
    {
        if (!condicao) Afirmar.Falhar(mensagem());
    }
}
