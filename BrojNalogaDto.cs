using System.ComponentModel.DataAnnotations;

namespace NalogZaUtovar;

public class BrojNalogaDto
{
    /// <summary>
    /// Broj naloga za utovar
    /// </summary>
    [Required]
    public int BrojNaloga { get; set; }
}
