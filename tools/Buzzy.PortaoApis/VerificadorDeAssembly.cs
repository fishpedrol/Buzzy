using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Buzzy.PortaoApis;

/// <summary>O que a leitura de um assembly gerenciado encontrou.</summary>
/// <param name="AssembliesReferenciados">Nomes da tabela AssemblyRef, para conferir dependências do produto.</param>
/// <param name="UsosRestritos">P/Invokes da lista proibida permitidos por <see cref="PortaoApis.UsosRestritos"/>.</param>
internal sealed record AnaliseDeAssembly(
    IReadOnlyList<Violacao> Violacoes,
    int PInvokes,
    int ReferenciasATipos,
    int ReferenciasAMembros,
    IReadOnlyList<string> AssembliesReferenciados,
    IReadOnlyList<UsoRestritoVisto> UsosRestritos);

/// <summary>
/// Confere os metadados de um assembly gerenciado com a lista proibida, sem carregá-lo:
/// declarações P/Invoke (tabela ImplMap), referências a tipos (TypeRef) e a membros (MemberRef),
/// e métodos de interfaces COM declaradas no próprio assembly. Chamadas por reflexão com nomes
/// em texto não são rastreadas aqui; a verificação do código-fonte cobre esses textos. Um
/// P/Invoke da lista proibida declarado no lugar de um uso restrito (<see cref="PortaoApis.UsosRestritos"/>)
/// não é violação: vai para o relatório como uso restrito.
/// </summary>
internal static class VerificadorDeAssembly
{
    private const int ProfundidadeMaxima = 64;

    public static AnaliseDeAssembly Verificar(string caminho)
    {
        using FileStream arquivo = File.OpenRead(caminho);
        using var pe = new PEReader(arquivo);
        if (!pe.HasMetadata)
            throw new BadImageFormatException($"{caminho} não é um assembly gerenciado (não tem metadados CLI).");
        return Verificar(caminho, pe.GetMetadataReader());
    }

    public static AnaliseDeAssembly Verificar(string caminho, MetadataReader md)
    {
        ArgumentNullException.ThrowIfNull(caminho);
        ArgumentNullException.ThrowIfNull(md);

        var violacoes = new List<Violacao>();
        var vistas = new HashSet<(string Api, Categoria Categoria)>();
        void Acusar(string codigo, Categoria categoria, string api, string detalhe, Regra? regra)
        {
            // Sobrecargas geram várias linhas MemberRef com o mesmo nome: uma violação basta.
            if (vistas.Add((api, categoria)))
                violacoes.Add(new Violacao(caminho, 0, 0, codigo, categoria, api, detalhe, regra));
        }

        string nomeDoAssembly = md.IsAssembly ? md.GetString(md.GetAssemblyDefinition().Name) : "";
        var usosRestritos = new List<UsoRestritoVisto>();
        int pinvokes = 0;
        foreach (MethodDefinitionHandle h in md.MethodDefinitions)
        {
            MethodDefinition metodo = md.GetMethodDefinition(h);
            string nomeDoMetodo = md.GetString(metodo.Name);

            if ((metodo.Attributes & MethodAttributes.PinvokeImpl) != 0)
            {
                pinvokes++;
                MethodImport importacao = metodo.GetImport();
                string modulo = importacao.Module.IsNil ? "" : md.GetString(md.GetModuleReference(importacao.Module).Name);
                string entrada = importacao.Name.IsNil ? "" : md.GetString(importacao.Name);
                if (entrada.Length == 0) entrada = nomeDoMetodo;
                string api = $"{modulo}!{entrada}";
                string onde = NomeDoMetodo(md, metodo);

                Regra? regra = ListaProibida.ProcurarNativa(modulo, entrada);
                UsoRestrito? uso = regra is null ? null
                    : UsosRestritos.NoBinario(nomeDoAssembly, modulo, entrada, NomeDoTipoDefinido(md, metodo.GetDeclaringType(), 0));
                if (uso is not null)
                    usosRestritos.Add(new UsoRestritoVisto(caminho, api, onde, regra!.Categoria, uso));
                else if (regra is not null)
                    Acusar(Codigos.PInvoke, regra.Categoria, api, $"P/Invoke em {onde}: {regra.Motivo}", regra);
                else if (entrada.StartsWith('#'))
                    Acusar(Codigos.PInvoke, Categoria.CodigoDinamico, api,
                        $"P/Invoke por ordinal em {onde}: o ponto de entrada não pode ser conferido com a lista proibida", null);
            }

            TypeDefinition declarante = md.GetTypeDefinition(metodo.GetDeclaringType());
            if ((declarante.Attributes & TypeAttributes.Interface) != 0
                && ListaProibida.ProcurarMetodoCom(nomeDoMetodo) is Regra com)
            {
                Acusar(Codigos.ReferenciaGerenciada, com.Categoria, NomeDoMetodo(md, metodo),
                    $"método de interface COM declarado no assembly: {com.Motivo}", com);
            }
        }

        int tipos = 0;
        foreach (TypeReferenceHandle h in md.TypeReferences)
        {
            tipos++;
            TypeReference tipo = md.GetTypeReference(h);
            // Um tipo aninhado aponta para o tipo externo, que também está na tabela e é
            // conferido por conta própria.
            if (tipo.ResolutionScope.Kind == HandleKind.TypeReference) continue;

            string nomeDoNamespace = md.GetString(tipo.Namespace);
            string nome = md.GetString(tipo.Name);
            if (ListaProibida.ProcurarTipo(nomeDoNamespace, nome) is Regra regra)
            {
                string detalhe = regra.Tipo == TipoDeRegra.NamespaceGerenciado
                    ? $"referência a tipo do namespace proibido {regra.Alvo}: {regra.Motivo}"
                    : $"referência ao tipo: {regra.Motivo}";
                Acusar(Codigos.ReferenciaGerenciada, regra.Categoria, ListaProibida.Juntar(nomeDoNamespace, nome), detalhe, regra);
            }
        }

        int membros = 0;
        foreach (MemberReferenceHandle h in md.MemberReferences)
        {
            membros++;
            MemberReference membro = md.GetMemberReference(h);
            string nome = md.GetString(membro.Name);
            string? tipo = NomeDoTipo(md, membro.Parent, 0);

            if (tipo is not null && ListaProibida.ProcurarMembro(tipo, nome) is Regra regra)
                Acusar(Codigos.ReferenciaGerenciada, regra.Categoria, $"{tipo}.{nome}", $"referência ao membro: {regra.Motivo}", regra);
            else if (ListaProibida.ProcurarMetodoCom(nome) is Regra com)
                Acusar(Codigos.ReferenciaGerenciada, com.Categoria, tipo is null ? nome : $"{tipo}.{nome}", $"referência a método COM: {com.Motivo}", com);
        }

        List<string> referenciados = [.. md.AssemblyReferences.Select(h => md.GetString(md.GetAssemblyReference(h).Name))];
        return new AnaliseDeAssembly(violacoes, pinvokes, tipos, membros, referenciados, usosRestritos);
    }

    /// <summary>
    /// Nome completo do tipo que declara um membro referenciado; nulo quando o pai não é um
    /// tipo (função global de um módulo, assinatura vararg de um método).
    /// </summary>
    private static string? NomeDoTipo(MetadataReader md, EntityHandle pai, int profundidade)
    {
        if (profundidade > ProfundidadeMaxima)
            throw new BadImageFormatException("Metadados com aninhamento de tipos circular ou profundo demais.");

        switch (pai.Kind)
        {
            case HandleKind.TypeReference:
            {
                TypeReference tipo = md.GetTypeReference((TypeReferenceHandle)pai);
                string nome = md.GetString(tipo.Name);
                return tipo.ResolutionScope.Kind == HandleKind.TypeReference
                    ? $"{NomeDoTipo(md, tipo.ResolutionScope, profundidade + 1)}+{nome}"
                    : ListaProibida.Juntar(md.GetString(tipo.Namespace), nome);
            }
            case HandleKind.TypeDefinition:
                return NomeDoTipoDefinido(md, (TypeDefinitionHandle)pai, profundidade);
            case HandleKind.TypeSpecification:
            {
                // Membro de tipo genérico instanciado (Lista<int>.Add): vale o tipo genérico.
                TypeSpecification especificacao = md.GetTypeSpecification((TypeSpecificationHandle)pai);
                BlobReader assinatura = md.GetBlobReader(especificacao.Signature);
                if (assinatura.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance) return null;
                if (assinatura.ReadSignatureTypeCode() != SignatureTypeCode.TypeHandle) return null;
                return NomeDoTipo(md, assinatura.ReadTypeHandle(), profundidade + 1);
            }
            default:
                return null;
        }
    }

    private static string NomeDoTipoDefinido(MetadataReader md, TypeDefinitionHandle h, int profundidade)
    {
        if (profundidade > ProfundidadeMaxima)
            throw new BadImageFormatException("Metadados com aninhamento de tipos circular ou profundo demais.");

        TypeDefinition tipo = md.GetTypeDefinition(h);
        string nome = md.GetString(tipo.Name);
        TypeDefinitionHandle externo = tipo.GetDeclaringType();
        return externo.IsNil
            ? ListaProibida.Juntar(md.GetString(tipo.Namespace), nome)
            : $"{NomeDoTipoDefinido(md, externo, profundidade + 1)}+{nome}";
    }

    private static string NomeDoMetodo(MetadataReader md, MethodDefinition metodo)
        => $"{NomeDoTipoDefinido(md, metodo.GetDeclaringType(), 0)}.{md.GetString(metodo.Name)}";
}
