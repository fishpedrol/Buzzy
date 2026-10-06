using Buzzy.Core.Personagem;

namespace Buzzy.App.Composicao;

/// <summary>
/// A edição deste build (DEC-044, item 2), o único lugar com a constante de compilação: <c>-p:BuzzyEdicao=publica</c> gera
/// a edição pública do download (sem as drogas ilícitas); sem ela, a completa. A raiz cria o núcleo por aqui, e a partida
/// escreve <see cref="NomeNoLog"/> no log de diagnóstico, que o empacotador confere.
/// </summary>
internal static class EdicaoDoBuild
{
#if BUZZY_PUBLICO
    internal const EdicaoDoBuzzy Atual = EdicaoDoBuzzy.Publica;
#else
    internal const EdicaoDoBuzzy Atual = EdicaoDoBuzzy.Completa;
#endif

    /// <summary>O nome da edição no log (<c>edicao=publica</c> ou <c>edicao=completa</c>).</summary>
    internal static string NomeNoLog => Atual == EdicaoDoBuzzy.Publica ? "publica" : "completa";
}
