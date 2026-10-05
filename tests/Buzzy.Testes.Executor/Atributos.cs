namespace Buzzy.Testes;

/// <summary>Marca um método público sem parâmetros como teste.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class TesteAttribute : Attribute
{
}

/// <summary>
/// Teste que precisa de uma sessão de desktop: abre janelas, move o cursor ou depende do
/// estado real da tela. Só roda com <c>--integracao</c>, para nunca abrir nada na tela do
/// usuário sem aviso.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class IntegracaoAttribute : Attribute
{
}
