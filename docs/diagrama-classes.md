# Diagrama de Classes — Ronu (MVP)

Tradução das entidades do DER (`docs/der.png`) para classes C#, que serão usadas como Models/Entities no Entity Framework Core.

```csharp
public class Usuario
{
    public int Id { get; set; }
    public string Nome { get; set; }
    public string Email { get; set; }
    public string SenhaHash { get; set; }
    public decimal Altura { get; set; }
    public DateOnly DataNascimento { get; set; }
    public string Sexo { get; set; }

    public ICollection<UsuarioModalidade> UsuarioModalidades { get; set; }
    public ICollection<ObjetivoUsuario> ObjetivosUsuario { get; set; }
    public ICollection<PreferenciaAlimentar> PreferenciasAlimentares { get; set; }
    public ICollection<DietaIA> DietasIA { get; set; }
}

public class Modalidade
{
    public int Id { get; set; }
    public string Nome { get; set; }
    public decimal MetReferencia { get; set; }
    public ICollection<UsuarioModalidade> UsuarioModalidades { get; set; }
}

public class UsuarioModalidade
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; }
    public int ModalidadeId { get; set; }
    public Modalidade Modalidade { get; set; }
    public int FrequenciaSemanal { get; set; }
}

public class ObjetivoUsuario
{
    public int Id { get; set; }
    public decimal Peso { get; set; }
    public string Objetivo { get; set; }
    public DateTime DataRegistro { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; }
}

public class PreferenciaAlimentar
{
    public int Id { get; set; }
    public string Alimento { get; set; }
    public string Tipo { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; }
}

public class DietaIA
{
    public int Id { get; set; }
    public DateTime DataGeracao { get; set; }
    public string ConteudoJson { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; }
}
```