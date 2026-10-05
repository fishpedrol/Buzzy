namespace Buzzy.App;

/// <summary>Códigos de saída do Buzzy.exe, para testes e diagnóstico.</summary>
internal static class CodigosDeSaida
{
    internal const int Normal = 0;

    /// <summary>A topologia dos monitores não pôde ser lida na partida, mesmo após novas tentativas.</summary>
    internal const int TopologiaIlegivel = 3;

    /// <summary>
    /// O processo foi iniciado com privilégio de administrador. O Buzzy recusa rodar elevado
    /// (SECURITY.md 8, item 5).
    /// </summary>
    internal const int Elevado = 5;

    /// <summary>Os objetos da instância única não puderam ser criados nem abertos.</summary>
    internal const int InstanciaUnicaIndisponivel = 6;
}
