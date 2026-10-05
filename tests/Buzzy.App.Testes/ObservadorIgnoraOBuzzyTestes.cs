using System.IO;
using System.Text.RegularExpressions;
using Buzzy.Testes;

namespace Buzzy.App.Testes;

/// <summary>
/// Fase 8, passo F8-P10 (DEC-038, nota do observador): o painel de energia e as configurações são janelas do próprio Buzzy, e
/// com elas em primeiro plano o observador da tela cheia (DEC-034) e a curiosidade (DEC-037) não publicam nada. São duas
/// defesas: os dois ganchos são assinados com <c>WINEVENT_SKIPOWNPROCESS</c> (a troca para uma janela do Buzzy nem dispara
/// avaliação), e a leitura reconhece a thread da interface (<c>DoBuzzy</c>, testado em AgendaDaTelaCheiaTestes e
/// FocoDoPrimeiroPlanoTestes). Aqui, a primeira, na fonte; a integração confere o conjunto.
/// </summary>
internal sealed class ObservadorIgnoraOBuzzyTestes
{
    [Teste]
    public void Ganchos_PulamOProprioProcesso_EALeituraReconheceAThread()
    {
        string codigo = Regex.Replace(File.ReadAllText(Path.Combine(Caminhos.Raiz, "src", "Buzzy.App", "Plataforma", "ObservadorDeTelaCheia.cs")), @"//.*|/\*[\s\S]*?\*/", "");
        Afirmar.Contem("private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;", codigo);
        MatchCollection ganchos = Regex.Matches(codigo, @"Nativo\.SetWinEventHook\(([^;]*)\);");
        Afirmar.Igual(2, ganchos.Count, "os dois ganchos (primeiro plano e geometria)");
        foreach (Match g in ganchos)
            Afirmar.Verdadeiro(g.Groups[1].Value.TrimEnd().EndsWith("WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS", StringComparison.Ordinal), $"o gancho pula o próprio processo: {g.Groups[1].Value}");
        Afirmar.Contem("if (thread == _threadDaInterface) return new LeituraDoPrimeiroPlano(null, null, DoBuzzy: true);", codigo);
    }
}
