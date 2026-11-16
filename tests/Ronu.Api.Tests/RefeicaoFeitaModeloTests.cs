using Microsoft.EntityFrameworkCore;
using Ronu.Api.Models;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Refeição feita, etapa 1: o modelo. A marcação não tem chave estrangeira para a DietaIA (como a MetaDieta), então a
/// regra das 3 dietas não a leva junto. O banco em memória não tem cascata nem índice único: o que depende disso é
/// conferido no modelo, que é de onde a migration sai.
/// </summary>
public class RefeicaoFeitaModeloTests
{
    [Fact]
    public void A_Unica_Chave_Estrangeira_E_Para_O_Usuario_Com_Cascata()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var tipo = banco.Model.FindEntityType(typeof(RefeicaoFeita))!;

        var chave = Assert.Single(tipo.GetForeignKeys());
        Assert.Equal(typeof(Usuario), chave.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, chave.DeleteBehavior);
    }

    [Fact]
    public void O_Indice_Unico_Barra_A_Mesma_Refeicao_Duas_Vezes_No_Mesmo_Dia_E_Dieta()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var tipo = banco.Model.FindEntityType(typeof(RefeicaoFeita))!;

        var indice = Assert.Single(tipo.GetIndexes());
        Assert.True(indice.IsUnique);
        Assert.Equal(
            new[] { nameof(RefeicaoFeita.UsuarioId), nameof(RefeicaoFeita.Data), nameof(RefeicaoFeita.DietaIAId), nameof(RefeicaoFeita.RefeicaoIndice) },
            indice.Properties.Select(p => p.Name));
    }

    [Fact]
    public async Task A_Dieta_Sair_Do_Historico_De_3_Nao_Apaga_As_Marcacoes_Dela()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = new Usuario { Nome = "Teste", Email = "historico@exemplo.test", Estado = "SP" };
        banco.Usuarios.Add(usuario);
        await banco.SaveChangesAsync();
        var inicio = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        var dietas = Enumerable.Range(0, 3)
            .Select(i => new DietaIA { UsuarioId = usuario.Id, DataGeracao = inicio.AddDays(i * 7), ConteudoJson = "{}" })
            .ToList();
        banco.DietasIA.AddRange(dietas);
        await banco.SaveChangesAsync();
        var maisAntiga = dietas[0];
        banco.RefeicoesFeitas.Add(new RefeicaoFeita
        {
            UsuarioId = usuario.Id, DietaIAId = maisAntiga.Id, Data = DateOnly.FromDateTime(inicio), RefeicaoIndice = 0,
            Kcal = 450, RefeicoesNoDia = 5, MarcadaEm = inicio
        });
        await banco.SaveChangesAsync();

        await RepositorioDietaIA.AbrirEspacoAsync(banco, usuario.Id);
        await banco.SaveChangesAsync();

        Assert.False(await banco.DietasIA.AnyAsync(d => d.Id == maisAntiga.Id));
        var marcacao = Assert.Single(banco.RefeicoesFeitas);
        Assert.Equal(maisAntiga.Id, marcacao.DietaIAId);
    }
}
